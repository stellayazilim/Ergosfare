
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// Emits an assembly's Ergosfare registrations at compile time.
/// </summary>
/// <remarks>
/// <para>
/// Every user-declared type reaching a module marker — <c>ICommand</c>, <c>IQuery</c>,
/// <c>IEvent</c> — is discovered and named in a generated
/// <c>ErgosfareGeneratedRegistrations</c> class. Messages carry a marker themselves;
/// handlers and interceptors reach one through their contracts.
/// </para>
/// <para>
/// The generated code names constructs, not pipelines: messages through
/// <c>Register(typeof(T))</c> and participants through the module builders'
/// <c>RegisterParticipants</c> batch, both read against the composition tables baked here
/// rather than assembled from descriptors at run time. A package predating the batch
/// surface gets per-type registration instead. Generic definitions are named unbound, one
/// entry serving every instantiation, and a dispatch closes participants over the
/// arguments of the message it carries.
/// </para>
/// <para>
/// Referenced assemblies are scanned as well, so a library's handlers register through
/// the project consuming it. Only assemblies that reference Ergosfare are read — nothing
/// else can carry a marker — and Ergosfare's own are skipped, their contract interfaces
/// inheriting the markers. A type generated code cannot name, such as an internal one
/// this compilation has no <c>InternalsVisibleTo</c> for, is reported as ERGO002 rather
/// than dropped in silence. Setting the MSBuild property
/// <c>ErgosfareSourceGeneratorScanReferences</c> to <c>false</c> turns the scan off for a
/// project.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed partial class ErgosfareRegistrationGenerator : IIncrementalGenerator
{





    private const string ScanReferencesBuildProperty = "build_property.ErgosfareSourceGeneratorScanReferences";


    /// <summary>
    /// Wires the generator's inputs and registers its source output.
    /// </summary>
    /// <param name="context">The initialization context Roslyn supplies.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The plugin facade is its own output: it shares no input with the registration
        // emission below, and must add no tree to a compilation that does not declare
        // itself a plugin.
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

        // Reference scanning is on unless a project turns it off; the package's .props
        // file surfaces the MSBuild property to the generator as a build_property.
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            !provider.GlobalOptions.TryGetValue(ScanReferencesBuildProperty, out var value)
            || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase));

        // Referenced marker types, plus the participants that take their message as a type
        // parameter, closed over the messages their constraint admits. Both ask about the
        // whole compilation rather than one declaration, so they share a stage; each model
        // carries its own provenance, and merging them here leaves the positional Combine
        // chain below alone.
        var referencedTypes = context.CompilationProvider
            .Combine(scanReferences)
            .Select(static (pair, ct) =>
            {
                var referenced = pair.Right
                    ? ReferenceScanner.ScanReferencedAssemblies(pair.Left, ct)
                    : ImmutableArray<RegistrableTypeModel>.Empty;

                return referenced.AddRange(Monomorphizer.MonomorphizeOpenParticipants(pair.Left, ct));
            });

        // Every mediator dispatch in this compilation, with the static type of its message
        // argument: what the emitted manifest records, and the local half of the
        // reachability judgment.
        var dispatchSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsDispatchInvocationCandidate(node),
                static (ctx, ct) => TransformDispatchSite(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        // Hand-written registrations — Register<T>() and Register(typeof(T)) — reaching
        // the container the way the generated bulk call does, one type at a time. The
        // shapes that cannot be read, a computed type or a RegisterParticipants batch, are
        // collected too: they are what leaves the coverage evidence incomplete.
        var registrationSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsRegistrationInvocationCandidate(node),
                static (ctx, ct) => TransformRegistrationSite(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        // The container's default result adapter, when UseDefaultResultAdapter names it
        // literally in this compilation; the staged plans then bake the binding for the
        // slots it serves.
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
        // the scan like the type input — with scanning off the picture is knowingly
        // incomplete, and nothing said about the whole closure would hold.
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

        // The plugin methods this compilation can see. Gated on the scan like every other
        // reference-derived input: a plugin arrives as referenced metadata, so scanning
        // off produces the same shape as referencing no plugin at all — and that shape
        // emits nothing.
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
