
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Symbols;
using Stella.Ergosfare.SourceGenerator.Planning;
using Stella.Ergosfare.SourceGenerator.ResultAdapters;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     Incremental generator producing compile-time Ergosfare registrations. It discovers
///     every user-declared type assignable to a module marker interface (<c>ICommand</c>,
///     <c>IQuery</c>, <c>IEvent</c>) — messages, handlers and interceptors all inherit the
///     marker through their contracts — and emits an <c>ErgosfareGeneratedRegistrations</c>
///     class into the compilation.
/// </summary>
/// <remarks>
///     <para>
///     Registration names constructs and nothing more: messages through
///     <c>Register(typeof(T))</c>, pipeline participants batched through the module
///     builders' <c>RegisterParticipants</c>, both recorded as the container's selection
///     from the frozen composition table this generator also bakes. What each construct's
///     pipeline looks like is decided here, at compile time, not assembled from
///     descriptors at run time. Generic definitions are named unbound — one table entry
///     serves every instantiation, and the dispatch closes participants over the
///     dispatched message's arguments. Against older Ergosfare packages that lack the
///     batch surface, emission degrades to per-type <c>Register(Type)</c> calls.
///     </para>
///     <para>
///     Reference scanning: the generator also walks referenced assemblies for marker
///     types — the compile-time replacement for the removed runtime assembly scanning: a
///     library's handlers register through the consuming project's generated code. Only assemblies
///     that themselves reference Ergosfare are inspected (nothing else can implement a
///     marker), and Ergosfare's own assemblies are excluded because their handler contract
///     interfaces inherit the module markers. Types the generated code cannot name —
///     internal without <c>InternalsVisibleTo</c> covering this compilation — surface as
///     ERGO002 instead of diverging silently from the runtime scan. Opt out per project
///     with the <c>ErgosfareSourceGeneratorScanReferences=false</c> MSBuild property.
///     </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed partial class ErgosfareRegistrationGenerator : IIncrementalGenerator
{





    private const string ScanReferencesBuildProperty = "build_property.ErgosfareSourceGeneratorScanReferences";


    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The plugin facade: its own source output, emitted only for an assembly that
        // declares itself a plugin. Kept separate because it shares no input with the
        // registration emission below and must not add a tree to ordinary compilations.
        RegisterPluginFacade(context);
        RegisterPluginScanDiagnostics(context);

        var registrableTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { BaseList.Types.Count: > 0 },
                static (ctx, ct) => RegistrableTypeReader.Transform(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        var availability = context.CompilationProvider.Select(
            static (compilation, _) => new ModuleBuilderAvailabilityReader(compilation).Read());

        // Reference scanning is default-on; consumers opt out per project through the
        // ErgosfareSourceGeneratorScanReferences MSBuild property (surfaced to the
        // generator as a build_property by the package's .props file).
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            !provider.GlobalOptions.TryGetValue(ScanReferencesBuildProperty, out var value)
            || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase));

        // Referenced types, plus the monomorphized participants — the ones that take their
        // message as a type parameter, closed over the messages their constraint admits.
        // Both are compilation-wide questions rather than per-declaration ones, so they
        // share a stage; each model carries its own provenance, and joining the array here
        // keeps the positional Combine chain below untouched.
        var referencedTypes = context.CompilationProvider
            .Combine(scanReferences)
            .Select(static (pair, ct) =>
            {
                var referenced = pair.Right
                    ? ReferenceScanner.ScanReferencedAssemblies(pair.Left, ct)
                    : ImmutableArray<RegistrableTypeModel>.Empty;

                return referenced.AddRange(Monomorphizer.MonomorphizeOpenParticipants(pair.Left, ct));
            });

        // Dispatch sites of the current compilation: every mediator dispatch invocation
        // with the static type of its message argument — the manifest emission's payload
        // and the local half of the reachability judgment.
        var dispatchSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsDispatchInvocationCandidate(node),
                static (ctx, ct) => TransformDispatchSite(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        // Manual registration sites: Register<T>() / Register(typeof(T)) calls — the same
        // collection path as RegisterGenerated(), per type instead of in bulk — plus the
        // opaque shapes (a runtime-computed type, a RegisterParticipants batch) that make
        // coverage evidence incomplete.
        var registrationSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsRegistrationInvocationCandidate(node),
                static (ctx, ct) => TransformRegistrationSite(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        // The container's default result adapter, when its UseDefaultResultAdapter
        // callsite is a literal in this compilation — the staged plans then bake the
        // binding for the slots it serves.
        var defaultResultAdapterSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax
                {
                    ArgumentList.Arguments.Count: 1,
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "UseDefaultResultAdapter" },
                },
                static (ctx, ct) => ResultAdapterReader.TransformDefaultResultAdapterSite(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect();

        // The referenced assemblies' manifests: the closure half of the judgment. Gated on
        // reference scanning like the type scan — with scanning off, the composition is
        // deliberately incomplete and no closure-wide judgment is sound.
        var referencedSites = context.CompilationProvider
            .Combine(scanReferences)
            .Select(static (pair, ct) => pair.Right
                ? ScanReferencedManifests(pair.Left, ct)
                : DispatchManifestScanResult.Empty);

        var judgmentInputs = context.AnalyzerConfigOptionsProvider
            .Combine(scanReferences)
            .Combine(context.CompilationProvider.Select(
                static (compilation, _) => IsExecutableOutputKind(compilation.Options.OutputKind)))
            .Select(static (pair, _) => new JudgmentInputs(
                ReadCompositionRootOverride(pair.Left.Left),
                ReadTrimUnusedHandlers(pair.Left.Left),
                pair.Left.Right,
                pair.Right));

        // The plugin methods visible to this compilation. Gated on reference scanning like
        // every other reference-derived input: a plugin arrives as a referenced assembly's
        // metadata, so scanning off means no plugin, which is also the shape a compilation
        // that references none produces — and that shape emits nothing.
        var pluginInvocations = context.CompilationProvider
            .Combine(scanReferences)
            .Select(static (pair, ct) => ScanPluginInvocations(pair.Left, pair.Right, ct));

        context.RegisterSourceOutput(
            registrableTypes.Combine(availability).Combine(referencedTypes)
                .Combine(dispatchSites).Combine(registrationSites).Combine(referencedSites).Combine(judgmentInputs)
                .Combine(defaultResultAdapterSites).Combine(pluginInvocations),
            static (spc, pair) => RegistrationPipeline.Execute(
                spc,
                pair.Left.Left.Left.Left.Left.Left.Left.Left,
                pair.Left.Left.Left.Left.Left.Left.Left.Right,
                pair.Left.Left.Left.Left.Left.Left.Right,
                pair.Left.Left.Left.Left.Left.Right,
                pair.Left.Left.Left.Left.Right,
                pair.Left.Left.Left.Right,
                pair.Left.Left.Right,
                pair.Left.Right,
                pair.Right));
    }






























































}
