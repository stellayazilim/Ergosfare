using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.ResultAdapters;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
/// Reads result adapters off symbols.
/// </summary>
/// <remarks>
/// Both the adapter a message declares by attribute and the container-wide default named at a
/// <c>UseDefaultResultAdapter</c> call. What an adapter serves is flattened into a slot key
/// here, so everything downstream can ask without holding a symbol.
/// </remarks>
internal static class ResultAdapterReader
{
    private const string DependencyInjectionExtensionsNamespace =
        "Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection";

    /// <summary>
    /// Reads a dispatchable message's <c>[ResultAdapter]</c> annotation.
    /// </summary>
    /// <param name="symbol">The message to read.</param>
    /// <param name="dispatchResults">The results the message declares.</param>
    /// <param name="isCommand">Whether it is a command, and so also dispatched void.</param>
    /// <param name="currentAssembly">
    /// The compilation's assembly, or <c>null</c> for a message read from metadata.
    /// </param>
    /// <param name="hasIgnore">Set when the message carries <c>[IgnoreResultAdapter]</c>.</param>
    /// <returns>The annotated adapter, or <c>null</c> when the message annotates none.</returns>
    /// <remarks>
    /// Both attributes are inherited, matching how the runtime binding reads them: the base
    /// chain is walked, the most derived annotation wins, and the opt-out counts from any
    /// level.
    /// </remarks>
    internal static ResultAdapterModel? GetResultAdapterModel(
        INamedTypeSymbol symbol,
        ImmutableArray<DispatchResultModel> dispatchResults,
        bool isCommand,
        IAssemblySymbol? currentAssembly,
        out bool hasIgnore)
    {
        INamedTypeSymbol? adapterSymbol = null;
        var annotationFound = false;
        hasIgnore = false;

        for (var current = symbol; current is not null; current = current.BaseType)
        {
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeClass || !SymbolNaming.IsInNamespace(attributeClass, ContractNames.AttributeNamespace))
                {
                    continue;
                }

                switch (attributeClass.Name)
                {
                    case "IgnoreResultAdapterAttribute":
                        hasIgnore = true;
                        break;
                    case "ResultAdapterAttribute" when !annotationFound:
                        annotationFound = true;
                        adapterSymbol = attribute.ConstructorArguments.Length == 1
                            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
                            : null;
                        break;
                }
            }
        }

        if (!annotationFound)
        {
            return null;
        }

        if (adapterSymbol is null || adapterSymbol.TypeKind == TypeKind.Error)
        {
            // A typeof that does not resolve: nothing can bind it, so it becomes an adapter
            // serving no slot at all — which is what ERGO011 reports.
            return new ResultAdapterModel(
                TypeofExpression: string.Empty,
                DisplayName: adapterSymbol?.ToDisplayString() ?? "?",
                AdapterSlotsKey: string.Empty,
                MaterializerSlotsKey: string.Empty,
                IsInstantiable: false,
                IsBakeable: false,
                FitsDeclaredSlot: false);
        }

        var adapterSlotsKey = BuildAdapterSlotsKey(adapterSymbol, "IResultAdapter");
        var materializerSlotsKey = BuildAdapterSlotsKey(adapterSymbol, "IResultMaterializer");

        var hasPublicParameterlessConstructor = false;

        foreach (var constructor in adapterSymbol.InstanceConstructors)
        {
            if (constructor.Parameters.IsEmpty && constructor.DeclaredAccessibility == Accessibility.Public)
            {
                hasPublicParameterlessConstructor = true;
                break;
            }
        }

        // What the runtime binding's Activator.CreateInstance needs. Baking asks for that and
        // for a type the emitted plan can name.
        var isInstantiable = hasPublicParameterlessConstructor
            && !adapterSymbol.IsAbstract
            && !adapterSymbol.IsUnboundGenericType
            && adapterSymbol.TypeKind is TypeKind.Class or TypeKind.Struct;

        var isBakeable = isInstantiable && ConstructionAnalyzer.IsNameableClosedType(adapterSymbol, currentAssembly);

        // The slots the runtime probes for this message: each declared result, with a stream
        // probed through its enumerator, plus the Unit slot a command's void dispatch has.
        var fitsDeclaredSlot = isCommand && AdapterSlotKey.Contains(adapterSlotsKey, EmittedExpressions.Unit);

        if (!fitsDeclaredSlot)
        {
            foreach (var dispatchResult in dispatchResults)
            {
                var slot = dispatchResult.IsStream
                    ? EmittedExpressions.AsyncEnumerator + "<" + dispatchResult.ResultTypeExpression + ">"
                    : dispatchResult.ResultTypeExpression;

                if (AdapterSlotKey.Contains(adapterSlotsKey, slot))
                {
                    fitsDeclaredSlot = true;
                    break;
                }
            }
        }

        return new ResultAdapterModel(
            TypeofExpression: SymbolNaming.VerbatimTypeExpression(adapterSymbol),
            DisplayName: adapterSymbol.ToDisplayString(),
            AdapterSlotsKey: adapterSlotsKey,
            MaterializerSlotsKey: materializerSlotsKey,
            IsInstantiable: isInstantiable,
            IsBakeable: isBakeable,
            FitsDeclaredSlot: fitsDeclaredSlot);
    }

    /// <summary>
    /// Flattens the slots an adapter serves through one contract into a key.
    /// </summary>
    /// <param name="adapterSymbol">The adapter to read.</param>
    /// <param name="contractName">
    /// The single-parameter contract to look for, declared in Core.Abstractions.
    /// </param>
    /// <returns>
    /// Its result-type expressions joined by the slot separator, or an empty string when it
    /// implements the contract for none.
    /// </returns>
    internal static string BuildAdapterSlotsKey(INamedTypeSymbol adapterSymbol, string contractName)
    {
        StringBuilder? slots = null;

        foreach (var iface in adapterSymbol.AllInterfaces)
        {
            if (iface.Arity != 1 || iface.Name != contractName || !SymbolNaming.IsInNamespace(iface, ContractNames.CoreAbstractionsNamespace))
            {
                continue;
            }

            (slots ??= new StringBuilder()).Append(slots.Length == 0 ? string.Empty : "\x1f")
                .Append(SymbolNaming.VerbatimTypeExpression(iface.TypeArguments[0]));
        }

        return slots?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Reads one <c>UseDefaultResultAdapter</c> call.
    /// </summary>
    /// <param name="ctx">The invocation to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>
    /// The site's model, or <c>null</c> when the invocation is not that method.
    /// </returns>
    /// <remarks>
    /// A literal <c>typeof</c> gives the slots a closed adapter serves, or the patterns an
    /// open definition serves. Anything else — a variable, a conditional, a type that does
    /// not resolve — comes back opaque, which turns baking off for the whole compilation.
    /// </remarks>
    internal static DefaultResultAdapterSiteModel? TransformDefaultResultAdapterSite(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method
            || method.Name != "UseDefaultResultAdapter"
            || !SymbolNaming.IsInNamespace(method.ContainingType, DependencyInjectionExtensionsNamespace))
        {
            return null;
        }

        // The argument itself where there is one, so ERGO019 underlines what could not be
        // read rather than the whole call chain.
        var location = LocationInfo.From(invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].Expression
            : (SyntaxNode)invocation);

        if (invocation.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOf
            || ctx.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type is not INamedTypeSymbol adapterSymbol
            || adapterSymbol.TypeKind == TypeKind.Error)
        {
            return DefaultResultAdapterSiteModel.OpaqueAt(location);
        }

        // The unbound form, typeof(X<>), carries no interfaces, so the definition is read
        // instead. A type parameter on a containing type would need a nesting-aware closing,
        // and such a definition stays opaque rather than half-read.
        var definition = adapterSymbol.IsUnboundGenericType ? adapterSymbol.OriginalDefinition : adapterSymbol;

        for (var container = definition.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.Arity > 0)
            {
                return DefaultResultAdapterSiteModel.OpaqueAt(location);
            }
        }

        var isOpen = definition.IsGenericType && adapterSymbol.IsUnboundGenericType;

        var hasPublicParameterlessConstructor = false;

        foreach (var constructor in definition.InstanceConstructors)
        {
            if (constructor.Parameters.IsEmpty && constructor.DeclaredAccessibility == Accessibility.Public)
            {
                hasPublicParameterlessConstructor = true;
                break;
            }
        }

        var isBakeable = hasPublicParameterlessConstructor
            && !definition.IsAbstract
            && definition.TypeKind is TypeKind.Class or TypeKind.Struct
            && SymbolNaming.HasSpellableName(definition)
            && IsAccessibleChain(definition, ctx.SemanticModel.Compilation.Assembly)
            && (isOpen || ConstructionAnalyzer.IsNameableClosedType(adapterSymbol, ctx.SemanticModel.Compilation.Assembly));

        string parameterNamesKey;
        var baseExpression = SymbolNaming.VerbatimTypeExpression(definition);

        if (isOpen)
        {
            var names = new StringBuilder();

            foreach (var parameter in definition.TypeParameters)
            {
                names.Append(names.Length == 0 ? string.Empty : "\x1f").Append(parameter.Name);
            }

            parameterNamesKey = names.ToString();

            // "global::App.BoxAdapter<T>" becomes "global::App.BoxAdapter"; closing it over a
            // bound slot appends the argument list the match produced.
            var angle = baseExpression.IndexOf('<');

            if (angle < 0)
            {
                return DefaultResultAdapterSiteModel.OpaqueAt(location);
            }

            baseExpression = baseExpression.Substring(0, angle);
        }
        else
        {
            parameterNamesKey = string.Empty;
        }

        return new DefaultResultAdapterSiteModel(
            IsOpaque: false,
            BaseTypeExpression: baseExpression,
            IsOpenGeneric: isOpen,
            Arity: isOpen ? definition.Arity : 0,
            ParameterNamesKey: parameterNamesKey,
            AdapterSlotsKey: BuildAdapterSlotsKey(definition, "IResultAdapter"),
            MaterializerSlotsKey: BuildAdapterSlotsKey(definition, "IResultMaterializer"),
            IsBakeable: isBakeable,
            Location: location);
    }

    /// <summary>
    /// Reports whether generated code in this compilation can name a type.
    /// </summary>
    /// <param name="symbol">The type to test.</param>
    /// <param name="currentAssembly">The compilation's assembly.</param>
    /// <returns><c>true</c> when every level of its containing chain is reachable.</returns>
    /// <remarks>
    /// Each level must be public, or internal and declared in this compilation.
    /// </remarks>
    internal static bool IsAccessibleChain(INamedTypeSymbol symbol, IAssemblySymbol currentAssembly)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    break;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (!SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, currentAssembly))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reduces a compilation's <c>UseDefaultResultAdapter</c> calls to the one adapter they
    /// agree on.
    /// </summary>
    /// <param name="sites">The calls collected from this compilation.</param>
    /// <returns>
    /// The agreed adapter, or <c>null</c> when there is no call, one of them is opaque, or
    /// they name different adapters.
    /// </returns>
    /// <remarks>
    /// The last two answers are also a failed build — ERGO019 and ERGO020 —  so nothing
    /// downstream has to serve them; answering <c>null</c> only keeps the rest of the
    /// generator from reasoning over facts it could not read.
    /// </remarks>
    internal static DefaultResultAdapterSiteModel? ReduceDefaultResultAdapter(
        ImmutableArray<DefaultResultAdapterSiteModel> sites)
    {
        DefaultResultAdapterSiteModel? reduced = null;

        foreach (var site in sites)
        {
            if (site.IsOpaque)
            {
                return null;
            }

            if (reduced is null)
            {
                reduced = site;
                continue;
            }

            if (!reduced.NamesSameAdapterAs(site))
            {
                return null;
            }
        }

        return reduced;
    }

    /// <summary>
    /// Judges the compilation's <c>UseDefaultResultAdapter</c> calls.
    /// </summary>
    /// <param name="context">The context diagnostics are reported to.</param>
    /// <param name="sites">The calls collected from this compilation.</param>
    /// <remarks>
    /// Three things have to hold for the fallback to reach a generated table, and each is an
    /// error rather than a quieter path: the argument is a literal <c>typeof</c> the
    /// compilation resolves (ERGO019), the compilation names one adapter (ERGO020), and
    /// generated code can name and construct it (ERGO021). What the compiler cannot read
    /// here, nothing can answer at run time.
    /// </remarks>
    internal static void ReportDefaultResultAdapterSites(
        SourceProductionContext context, ImmutableArray<DefaultResultAdapterSiteModel> sites)
    {
        DefaultResultAdapterSiteModel? first = null;

        foreach (var site in sites)
        {
            if (site.IsOpaque)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.OpaqueDefaultResultAdapter, site.Location?.ToLocation()));
                continue;
            }

            if (!site.IsBakeable)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.UnbakeableDefaultResultAdapter,
                    site.Location?.ToLocation(),
                    site.BaseTypeExpression));
            }

            if (first is null)
            {
                first = site;
                continue;
            }

            if (!first.NamesSameAdapterAs(site))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.ConflictingDefaultResultAdapters,
                    site.Location?.ToLocation(),
                    site.BaseTypeExpression,
                    first.BaseTypeExpression));
            }
        }
    }

    /// <summary>
    /// Reports whether a message declares a result slot no adapter serves.
    /// </summary>
    /// <param name="message">The message to test.</param>
    /// <param name="defaultAdapter">The container's default adapter.</param>
    /// <param name="unservedSlotExpression">
    /// The unserved result type, when this returns <c>true</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when the message declares at least one non-stream result and neither the
    /// native adapters nor the default serves any of them.
    /// </returns>
    /// <remarks>
    /// What ERGO013 reports. A void or stream-only message never qualifies: it has no result
    /// value to carry a failure in, so throwing is its contract rather than a misfit.
    /// </remarks>
    internal static bool HasUnservedResultSlots(
        RegistrableTypeModel message, DefaultResultAdapterSiteModel defaultAdapter, out string unservedSlotExpression)
    {
        unservedSlotExpression = string.Empty;
        var sawResultSlot = false;

        foreach (var dispatchResult in message.DispatchResults)
        {
            if (dispatchResult.IsStream)
            {
                continue;
            }

            sawResultSlot = true;
            unservedSlotExpression = dispatchResult.ResultTypeExpression;

            if (NativeResultAdapters.TryGetExpression(dispatchResult.ResultTypeExpression, out _)
                || new DefaultResultAdapterBinder(defaultAdapter).TryBind(dispatchResult.ResultTypeExpression, out _, out _))
            {
                return false;
            }
        }

        return sawResultSlot;
    }
}
