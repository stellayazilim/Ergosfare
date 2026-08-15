using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.ResultAdapters;

namespace Stella.Ergosfare.SourceGenerator.Symbols;

/// <summary>
///     Reads result adapters off symbols: the ones a participant declares by attribute, and
///     the container-wide default named at a <c>UseDefaultResultAdapter</c> callsite. What
///     an adapter serves is flattened into a slot key here, so everything downstream can ask
///     without a symbol in hand.
/// </summary>

internal static class ResultAdapterReader
{
    private const string DependencyInjectionExtensionsNamespace =
        "Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection";

    /// <summary>
    ///     Projects a dispatchable message's <c>[ResultAdapter]</c> annotation, or
    ///     <c>null</c> when there is none, and reports whether the message carries
    ///     <c>[IgnoreResultAdapter]</c>. The runtime binding reads both attributes with
    ///     inheritance (<c>GetCustomAttribute</c>'s default), so the mirror walks the base
    ///     chain — the most derived annotation wins; the opt-out wins from any level.
    /// </summary>
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
            // A typeof the model cannot resolve: nothing can ever bind — ERGO011
            // material carrying no slots at all.
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

        // What the runtime binding's Activator.CreateInstance requires; bakeability
        // additionally requires the emitted plan to be able to name the type.
        var isInstantiable = hasPublicParameterlessConstructor
            && !adapterSymbol.IsAbstract
            && !adapterSymbol.IsUnboundGenericType
            && adapterSymbol.TypeKind is TypeKind.Class or TypeKind.Struct;

        var isBakeable = isInstantiable && ConstructionAnalyzer.IsNameableClosedType(adapterSymbol, currentAssembly);

        // The message's runtime-probed slots: every declared result (a stream probes its
        // enumerator), plus the Unit lane every command's void dispatch shape carries.
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
    ///     The result-type expressions of the adapter's implementations of the given
    ///     Core.Abstractions arity-1 contract, joined with the model's slot separator.
    /// </summary>
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
    ///     Projects one <c>UseDefaultResultAdapter(...)</c> callsite. A literal
    ///     <c>typeof</c> projects the adapter's served slots (closed) or slot patterns
    ///     (open definition); anything else — a variable, a conditional, an unresolvable
    ///     type — projects the opaque marker, which turns baking off compilation-wide.
    /// </summary>
    internal static DefaultResultAdapterSiteModel? TransformDefaultResultAdapterSite(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method
            || method.Name != "UseDefaultResultAdapter"
            || !SymbolNaming.IsInNamespace(method.ContainingType, DependencyInjectionExtensionsNamespace))
        {
            return null;
        }

        if (invocation.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOf
            || ctx.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type is not INamedTypeSymbol adapterSymbol
            || adapterSymbol.TypeKind == TypeKind.Error)
        {
            return DefaultResultAdapterSiteModel.Opaque;
        }

        // The unbound form (typeof(X<>)) carries no interfaces; project its definition.
        // Type parameters on a containing type would need a nesting-aware closing — such
        // definitions stay opaque rather than half-modeled.
        var definition = adapterSymbol.IsUnboundGenericType ? adapterSymbol.OriginalDefinition : adapterSymbol;

        for (var container = definition.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.Arity > 0)
            {
                return DefaultResultAdapterSiteModel.Opaque;
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

            // "global::App.BoxAdapter<T>" → "global::App.BoxAdapter"; the closing per
            // bound slot re-appends the unified argument list.
            var angle = baseExpression.IndexOf('<');

            if (angle < 0)
            {
                return DefaultResultAdapterSiteModel.Opaque;
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
            IsBakeable: isBakeable);
    }

    /// <summary>
    ///     Whether every level of the containing-type chain is nameable from generated
    ///     code in the current compilation — public, or at-least-internal within it.
    /// </summary>
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
    ///     Reduces the compilation's <c>UseDefaultResultAdapter</c> callsites to one
    ///     modeled view, or <c>null</c> when the mirror must stand down: no site at all,
    ///     an opaque site, or sites disagreeing on the adapter. Standing down is never
    ///     wrong — the runtime tier serves the default, the adapter-identity gate keeps
    ///     unbaked plans off served slots, and no ERGO013 judgment runs over facts the
    ///     mirror cannot see. Baking additionally requires <c>IsBakeable</c>.
    /// </summary>
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

            if (!reduced.Equals(site))
            {
                return null;
            }
        }

        return reduced;
    }

    /// <summary>
    ///     The ERGO013 predicate: a result-bearing message none of whose non-stream
    ///     result slots any adapter tier serves — not native, not the configured default.
    ///     Void and stream-only messages never qualify: they have no result value to
    ///     carry a failure in, so throwing is their inherent contract, not a misfit.
    /// </summary>
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
