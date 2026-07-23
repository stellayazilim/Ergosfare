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
///     Incremental generator producing compile-time Ergosfare registrations (Phase 1 of the
///     source-generation roadmap). It discovers every user-declared type assignable to a
///     module marker interface (<c>ICommand</c>, <c>IQuery</c>, <c>IEvent</c>) — messages,
///     handlers and interceptors all inherit the marker through their contracts — and emits
///     an <c>ErgosfareGeneratedRegistrations</c> class whose methods register those types
///     with direct <c>typeof</c> calls. This replaces the reflection-based
///     <c>RegisterFromAssembly</c> scan; the runtime <c>Register(Type)</c> path stays as the
///     fallback and both paths are mutually idempotent in the registry.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ErgosfareRegistrationGenerator : IIncrementalGenerator
{
    private const string CommandMarkerName = "ICommand";
    private const string CommandMarkerNamespace = "Stella.Ergosfare.Commands.Abstractions";
    private const string QueryMarkerName = "IQuery";
    private const string QueryMarkerNamespace = "Stella.Ergosfare.Queries.Abstractions";
    private const string EventMarkerName = "IEvent";
    private const string EventMarkerNamespace = "Stella.Ergosfare.Events.Abstractions";

    private const string MessageRegistryMetadataName = "Stella.Ergosfare.Core.Abstractions.Registry.IMessageRegistry";
    private const string CommandBuilderMetadataName = "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";
    private const string QueryBuilderMetadataName = "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";
    private const string EventBuilderMetadataName = "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

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
            new ModuleBuilderAvailability(
                HasMessageRegistry: compilation.GetTypeByMetadataName(MessageRegistryMetadataName) is not null,
                HasCommandModuleBuilder: compilation.GetTypeByMetadataName(CommandBuilderMetadataName) is not null,
                HasQueryModuleBuilder: compilation.GetTypeByMetadataName(QueryBuilderMetadataName) is not null,
                HasEventModuleBuilder: compilation.GetTypeByMetadataName(EventBuilderMetadataName) is not null));

        context.RegisterSourceOutput(
            registrableTypes.Combine(availability),
            static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

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

        return new RegistrableTypeModel(
            TypeofExpression: BuildTypeofExpression(symbol),
            DisplayName: symbol.ToDisplayString(),
            IsCommand: isCommand,
            IsQuery: isQuery,
            IsEvent: isEvent,
            IsAccessible: isAccessible,
            Location: isAccessible ? null : LocationInfo.From(symbol));
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> models,
        ModuleBuilderAvailability availability)
    {
        var seen = new HashSet<string>();
        var all = new List<string>();
        var commands = new List<string>();
        var queries = new List<string>();
        var events = new List<string>();

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

            all.Add(model.TypeofExpression);

            if (model.IsCommand)
            {
                commands.Add(model.TypeofExpression);
            }

            if (model.IsQuery)
            {
                queries.Add(model.TypeofExpression);
            }

            if (model.IsEvent)
            {
                events.Add(model.TypeofExpression);
            }
        }

        if (all.Count == 0)
        {
            return;
        }

        // Deterministic output regardless of declaration/discovery order.
        all.Sort(StringComparer.Ordinal);
        commands.Sort(StringComparer.Ordinal);
        queries.Sort(StringComparer.Ordinal);
        events.Sort(StringComparer.Ordinal);

        var source = RegistrationEmitter.Emit(all, commands, queries, events, availability, GeneratorVersion);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
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
