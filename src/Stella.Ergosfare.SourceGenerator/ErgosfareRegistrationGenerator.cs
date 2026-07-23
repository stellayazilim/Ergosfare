using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Incremental generator producing compile-time Ergosfare registrations. It discovers
///     every user-declared type assignable to a module marker interface (<c>ICommand</c>,
///     <c>IQuery</c>, <c>IEvent</c>) — messages, handlers and interceptors all inherit the
///     marker through their contracts — and emits an <c>ErgosfareGeneratedRegistrations</c>
///     class into the compilation.
/// </summary>
/// <remarks>
///     Phase 2: for types with handler contracts, the generator pre-computes the handler
///     descriptors (message type, result carrier, weight, groups) that the runtime
///     descriptor builders would otherwise derive reflectively, and registers them through
///     <c>IMessageRegistry.RegisterDescriptors</c> / the module builders'
///     <c>RegisterDescriptors</c>. Plain messages and open generic types (whose contract
///     type arguments cannot appear in <c>typeof</c>) fall back to <c>Register(Type)</c>;
///     both paths are mutually idempotent in the registry. Against older Ergosfare packages
///     that lack the descriptor surface, emission degrades to pure <c>Register(Type)</c>
///     calls.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ErgosfareRegistrationGenerator : IIncrementalGenerator
{
    private const string CommandMarkerName = "ICommand";
    private const string CommandMarkerNamespace = "Stella.Ergosfare.Commands.Abstractions";
    private const string QueryMarkerName = "IQuery";
    private const string QueryMarkerNamespace = "Stella.Ergosfare.Queries.Abstractions";
    private const string EventMarkerName = "IEvent";
    private const string EventMarkerNamespace = "Stella.Ergosfare.Events.Abstractions";

    private const string HandlerContractNamespace = "Stella.Ergosfare.Core.Abstractions.Handlers";
    private const string AttributeNamespace = "Stella.Ergosfare.Core.Abstractions.Attributes";

    private const string MessageRegistryMetadataName = "Stella.Ergosfare.Core.Abstractions.Registry.IMessageRegistry";
    private const string DescriptorFactoryMetadataName = "Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.HandlerDescriptors";
    private const string CommandBuilderMetadataName = "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";
    private const string QueryBuilderMetadataName = "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";
    private const string EventBuilderMetadataName = "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

    private const string ValueTaskExpression = "global::System.Threading.Tasks.ValueTask";

    private static readonly string GeneratorVersion =
        typeof(ErgosfareRegistrationGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrableTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { BaseList.Types.Count: > 0 },
                static (ctx, ct) => Transform(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        var availability = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var commandBuilder = compilation.GetTypeByMetadataName(CommandBuilderMetadataName);
            var queryBuilder = compilation.GetTypeByMetadataName(QueryBuilderMetadataName);
            var eventBuilder = compilation.GetTypeByMetadataName(EventBuilderMetadataName);

            return new ModuleBuilderAvailability(
                HasMessageRegistry: compilation.GetTypeByMetadataName(MessageRegistryMetadataName) is not null,
                HasCommandModuleBuilder: commandBuilder is not null,
                HasQueryModuleBuilder: queryBuilder is not null,
                HasEventModuleBuilder: eventBuilder is not null,
                HasDescriptorFactory: compilation.GetTypeByMetadataName(DescriptorFactoryMetadataName) is not null,
                CommandBuilderHasRegisterDescriptors: HasRegisterDescriptors(commandBuilder),
                QueryBuilderHasRegisterDescriptors: HasRegisterDescriptors(queryBuilder),
                EventBuilderHasRegisterDescriptors: HasRegisterDescriptors(eventBuilder));
        });

        context.RegisterSourceOutput(
            registrableTypes.Combine(availability),
            static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static bool HasRegisterDescriptors(INamedTypeSymbol? builder)
        => builder is not null && !builder.GetMembers("RegisterDescriptors").IsEmpty;

    /// <summary>
    ///     Projects a candidate type declaration to its registration model, or <c>null</c>
    ///     when the type carries no Ergosfare marker. Runs per declaration; partial types
    ///     may yield duplicates, which <see cref="Execute"/> dedupes.
    /// </summary>
    private static RegistrableTypeModel? Transform(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)ctx.Node, ct) is not { } symbol)
        {
            return null;
        }

        // Static classes cannot implement interfaces; implicitly declared symbols are
        // compiler artifacts. Neither is registrable.
        if (symbol.IsStatic || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        var isCommand = false;
        var isQuery = false;
        var isEvent = false;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity != 0)
            {
                continue;
            }

            switch (iface.Name)
            {
                case CommandMarkerName when IsInNamespace(iface, CommandMarkerNamespace):
                    isCommand = true;
                    break;
                case QueryMarkerName when IsInNamespace(iface, QueryMarkerNamespace):
                    isQuery = true;
                    break;
                case EventMarkerName when IsInNamespace(iface, EventMarkerNamespace):
                    isEvent = true;
                    break;
            }
        }

        if (!isCommand && !isQuery && !isEvent)
        {
            return null;
        }

        var isAccessible = IsAccessibleFromGeneratedCode(symbol);

        return new RegistrableTypeModel
        {
            TypeofExpression = BuildTypeofExpression(symbol),
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = isAccessible,
            Location = isAccessible ? null : LocationInfo.From(symbol),
            Weight = GetWeight(symbol),
            GroupsExpression = GetGroupsExpression(symbol),
            Descriptors = isAccessible ? BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty,
        };
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> models,
        ModuleBuilderAvailability availability)
    {
        var seen = new HashSet<string>();
        var types = new List<RegistrableTypeModel>();

        foreach (var model in models)
        {
            if (!seen.Add(model.TypeofExpression))
            {
                continue;
            }

            if (!model.IsAccessible)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.InaccessibleRegistrableType,
                    model.Location?.ToLocation(),
                    model.DisplayName));
                continue;
            }

            types.Add(model);
        }

        if (types.Count == 0)
        {
            return;
        }

        // Deterministic output regardless of declaration/discovery order.
        types.Sort(static (x, y) => string.CompareOrdinal(x.TypeofExpression, y.TypeofExpression));

        var source = RegistrationEmitter.Emit(types, availability, GeneratorVersion);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>
    ///     Pre-computes the handler descriptors for the type's handler contracts, mirroring
    ///     the runtime descriptor builders exactly: main handlers keep their declared
    ///     message types verbatim (sync contracts first, then result-less async, then
    ///     result-producing async, no dedupe), interceptors normalize generic messages to
    ///     their definitions and dedupe per (message, result) pair with the synchronous
    ///     pattern winning. Open generic types return an empty set — their contract type
    ///     arguments contain type parameters, which cannot appear in <c>typeof</c> — and
    ///     fall back to runtime registration.
    /// </summary>
    private static ImmutableArray<DescriptorModel> BuildDescriptors(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return ImmutableArray<DescriptorModel>.Empty;
            }
        }

        List<DescriptorModel>? mainSync = null;
        List<DescriptorModel>? mainAsyncVoid = null;
        List<DescriptorModel>? mainAsyncResult = null;
        List<DescriptorModel>? preSync = null;
        List<DescriptorModel>? preAsync = null;
        List<DescriptorModel>? postSync = null;
        List<DescriptorModel>? postAsyncTyped = null;
        List<DescriptorModel>? postAsyncAgnostic = null;
        List<DescriptorModel>? exceptionSync = null;
        List<DescriptorModel>? exceptionAsyncTyped = null;
        List<DescriptorModel>? exceptionAsyncAgnostic = null;
        List<DescriptorModel>? finalSync = null;
        List<DescriptorModel>? finalAsyncTyped = null;
        List<DescriptorModel>? finalAsyncAgnostic = null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity is not (1 or 2) || !IsInNamespace(iface, HandlerContractNamespace))
            {
                continue;
            }

            var arguments = iface.TypeArguments;

            switch (iface.Name)
            {
                case "IHandler" when iface.Arity == 2:
                    (mainSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.MainHandler,
                        VerbatimTypeExpression(arguments[0]),
                        VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncHandler" when iface.Arity == 1:
                    (mainAsyncVoid ??= []).Add(new DescriptorModel(
                        DescriptorKind.MainHandler,
                        VerbatimTypeExpression(arguments[0]),
                        ValueTaskExpression));
                    break;
                case "IAsyncHandler" when iface.Arity == 2:
                    (mainAsyncResult ??= []).Add(new DescriptorModel(
                        DescriptorKind.MainHandler,
                        VerbatimTypeExpression(arguments[0]),
                        ValueTaskExpression + "<" + VerbatimTypeExpression(arguments[1]) + ">"));
                    break;

                case "IPreInterceptor" when iface.Arity == 1:
                    (preSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.PreInterceptor, NormalizedTypeExpression(arguments[0]), null));
                    break;
                case "IAsyncPreInterceptor" when iface.Arity == 1:
                    (preAsync ??= []).Add(new DescriptorModel(
                        DescriptorKind.PreInterceptor, NormalizedTypeExpression(arguments[0]), null));
                    break;

                case "IPostInterceptor" when iface.Arity == 2:
                    (postSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.PostInterceptor, NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncPostInterceptor" when iface.Arity == 2:
                    (postAsyncTyped ??= []).Add(new DescriptorModel(
                        DescriptorKind.PostInterceptor, NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncPostInterceptor" when iface.Arity == 1:
                    (postAsyncAgnostic ??= []).Add(new DescriptorModel(
                        DescriptorKind.PostInterceptor, NormalizedTypeExpression(arguments[0]), "object"));
                    break;

                case "IExceptionInterceptor" when iface.Arity == 2:
                    (exceptionSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.ExceptionInterceptor, NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncExceptionInterceptor" when iface.Arity == 2:
                    (exceptionAsyncTyped ??= []).Add(new DescriptorModel(
                        DescriptorKind.ExceptionInterceptor, NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncExceptionInterceptor" when iface.Arity == 1:
                    (exceptionAsyncAgnostic ??= []).Add(new DescriptorModel(
                        DescriptorKind.ExceptionInterceptor, NormalizedTypeExpression(arguments[0]), "object"));
                    break;

                case "IFinalInterceptor" when iface.Arity == 2:
                    (finalSync ??= []).Add(new DescriptorModel(
                        DescriptorKind.FinalInterceptor, NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncFinalInterceptor" when iface.Arity == 2:
                    (finalAsyncTyped ??= []).Add(new DescriptorModel(
                        DescriptorKind.FinalInterceptor, NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])));
                    break;
                case "IAsyncFinalInterceptor" when iface.Arity == 1:
                    (finalAsyncAgnostic ??= []).Add(new DescriptorModel(
                        DescriptorKind.FinalInterceptor, NormalizedTypeExpression(arguments[0]), "object"));
                    break;
            }
        }

        var result = ImmutableArray.CreateBuilder<DescriptorModel>();

        // Main handlers: runtime builder order, no dedupe.
        AppendAll(result, mainSync);
        AppendAll(result, mainAsyncVoid);
        AppendAll(result, mainAsyncResult);

        // Interceptors: runtime builder order with first-wins dedupe per (message, result).
        AppendDeduped(result, preSync, preAsync, null);
        AppendDeduped(result, postSync, postAsyncTyped, postAsyncAgnostic);
        AppendDeduped(result, exceptionSync, exceptionAsyncTyped, exceptionAsyncAgnostic);
        AppendDeduped(result, finalSync, finalAsyncTyped, finalAsyncAgnostic);

        return result.ToImmutable();
    }

    private static void AppendAll(ImmutableArray<DescriptorModel>.Builder result, List<DescriptorModel>? bucket)
    {
        if (bucket is null)
        {
            return;
        }

        foreach (var descriptor in bucket)
        {
            result.Add(descriptor);
        }
    }

    private static void AppendDeduped(
        ImmutableArray<DescriptorModel>.Builder result,
        List<DescriptorModel>? first,
        List<DescriptorModel>? second,
        List<DescriptorModel>? third)
    {
        if (first is null && second is null && third is null)
        {
            return;
        }

        var seen = new HashSet<(string Message, string? Result)>();

        AppendBucket(result, first, seen);
        AppendBucket(result, second, seen);
        AppendBucket(result, third, seen);

        static void AppendBucket(
            ImmutableArray<DescriptorModel>.Builder result,
            List<DescriptorModel>? bucket,
            HashSet<(string Message, string? Result)> seen)
        {
            if (bucket is null)
            {
                return;
            }

            foreach (var descriptor in bucket)
            {
                if (seen.Add((descriptor.MessageTypeExpression, descriptor.ResultTypeExpression)))
                {
                    result.Add(descriptor);
                }
            }
        }
    }

    /// <summary>
    ///     The fully qualified <c>typeof</c> argument for a type exactly as declared —
    ///     constructed generics included.
    /// </summary>
    private static string VerbatimTypeExpression(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    ///     The fully qualified <c>typeof</c> argument for a type, with generic types
    ///     normalized to their unbound definitions — mirroring the interceptor descriptor
    ///     builders' <c>GetGenericTypeDefinition()</c> normalization.
    /// </summary>
    private static string NormalizedTypeExpression(ITypeSymbol type)
        => type is INamedTypeSymbol { IsGenericType: true } named
            ? BuildTypeofExpression(named.OriginalDefinition)
            : VerbatimTypeExpression(type);

    /// <summary>Reads the <c>[Weight]</c> attribute value, or 0 when undeclared.</summary>
    private static uint GetWeight(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "WeightAttribute" } attributeClass
                && IsInNamespace(attributeClass, AttributeNamespace)
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is uint weight)
            {
                return weight;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Builds the emitted C# array expression for the <c>[Group]</c> names, or
    ///     <c>null</c> when the type declares none (the descriptor factory then applies the
    ///     default group, matching the reflection path).
    /// </summary>
    private static string? GetGroupsExpression(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "GroupAttribute" } attributeClass
                || !IsInNamespace(attributeClass, AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return null;
            }

            var sb = new StringBuilder("new string[] { ");

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Value is not string name)
                {
                    continue;
                }

                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(SymbolDisplay.FormatLiteral(name, quote: true));
            }

            sb.Append(" }");
            return sb.ToString();
        }

        return null;
    }

    /// <summary>
    ///     Whether generated code — a sibling top-level type in the same assembly — can
    ///     reference the type. Private/protected members of other types and file-local
    ///     types cannot be named from the generated file.
    /// </summary>
    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            if (current.DeclaredAccessibility is Accessibility.Private
                or Accessibility.Protected
                or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Builds the fully qualified <c>typeof</c> argument for a type, walking the
    ///     containing-type chain so nested types render correctly. Generic definitions use
    ///     the unbound form (<c>Foo&lt;,&gt;</c>) — mixing bound and unbound levels is not
    ///     legal C#, and every discovered type is a definition, never a constructed generic.
    /// </summary>
    private static string BuildTypeofExpression(INamedTypeSymbol symbol)
    {
        var parts = new Stack<string>();

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            parts.Push(current.Arity == 0
                ? current.Name
                : current.Name + "<" + new string(',', current.Arity - 1) + ">");
        }

        var sb = new StringBuilder("global::");

        if (symbol.ContainingNamespace is { IsGlobalNamespace: false } ns)
        {
            sb.Append(ns.ToDisplayString()).Append('.');
        }

        var first = true;
        foreach (var part in parts)
        {
            if (!first)
            {
                sb.Append('.');
            }

            sb.Append(part);
            first = false;
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Checks that a symbol lives exactly in the given dotted namespace.
    /// </summary>
    private static bool IsInNamespace(INamedTypeSymbol symbol, string expectedNamespace)
    {
        var ns = symbol.ContainingNamespace;

        for (var end = expectedNamespace.Length; end > 0;)
        {
            if (ns is null || ns.IsGlobalNamespace)
            {
                return false;
            }

            var start = expectedNamespace.LastIndexOf('.', end - 1) + 1;

            if (ns.Name.Length != end - start
                || string.CompareOrdinal(expectedNamespace, start, ns.Name, 0, ns.Name.Length) != 0)
            {
                return false;
            }

            ns = ns.ContainingNamespace;
            end = start - 1;
        }

        return ns is { IsGlobalNamespace: true };
    }
}
