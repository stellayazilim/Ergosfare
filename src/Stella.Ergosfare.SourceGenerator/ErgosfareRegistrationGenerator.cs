
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
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
///     ERGOSG002 instead of diverging silently from the runtime scan. Opt out per project
///     with the <c>ErgosfareSourceGeneratorScanReferences=false</c> MSBuild property.
///     </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed partial class ErgosfareRegistrationGenerator : IIncrementalGenerator
{
    private const string CommandMarkerName = "ICommand";
    private const string CommandMarkerNamespace = "Stella.Ergosfare.Commands.Abstractions";
    private const string QueryMarkerName = "IQuery";
    private const string QueryMarkerNamespace = "Stella.Ergosfare.Queries.Abstractions";
    private const string EventMarkerName = "IEvent";
    private const string EventMarkerNamespace = "Stella.Ergosfare.Events.Abstractions";

    private const string HandlerContractNamespace = "Stella.Ergosfare.Core.Abstractions.Handlers";
    private const string ExceptionFilterContractName = "IExceptionInterceptorFilter";
    private const string AttributeNamespace = "Stella.Ergosfare.Core.Abstractions.Attributes";

    /// <summary>
    ///     Mirror of <c>GroupAttribute.DefaultGroupName</c>: the group a participant
    ///     without <c>[Group]</c> is registered under.
    /// </summary>
    private const string DefaultGroupName = "default";

    // The result-adapter binding mirror: the native carriers, their built-in adapters and
    // the runtime-probed enumerator slot of stream pipelines. The carriers live in the
    // Core.Abstractions root; only their adapters live in .Results.
    private const string NativeResultExpression = "global::Stella.Ergosfare.Core.Abstractions.Result";
    private const string NativeResultAdapterExpression = "global::Stella.Ergosfare.Core.Abstractions.Results.ResultExceptionAdapter";
    private const string AsyncEnumeratorExpression = "global::System.Collections.Generic.IAsyncEnumerator";

    private const string CompositionCatalogMetadataName = "Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenCompositionCatalog";
    private const string DispatchRootsMetadataName = "Stella.Ergosfare.Core.Abstractions.DispatchRoots.GeneratedDispatchRoots";
    private const string CommandBuilderMetadataName = "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";
    private const string QueryBuilderMetadataName = "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";
    private const string EventBuilderMetadataName = "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

    private const string ValueTaskExpression = "global::System.Threading.Tasks.ValueTask";

    /// <summary>
    ///     The result representation of a pipeline that produces none — what the void
    ///     plans' interceptor arms match against. <see cref="ValueTaskExpression"/> stays
    ///     the completion signal (a void handler's return carrier), which is what the
    ///     handler-descriptor gates below keep checking.
    /// </summary>
    private const string UnitExpression = "global::Stella.Ergosfare.Core.Abstractions.Unit";
    private const string StagedVoidPlanMetadataName = "Stella.Ergosfare.Core.Abstractions.StagedPlans.StagedVoidPlan";
    private const string ServiceProviderExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";
    private const string KeyedServiceExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions";

    private const string ScanReferencesBuildProperty = "build_property.ErgosfareSourceGeneratorScanReferences";
    private const string ErgosfareAssemblyNamePrefix = "Stella.Ergosfare";

    // Per-assembly opt-in that force-includes an assembly matching the reserved prefix.
    // Surfaced from the ErgosfareSourceGeneratorForceScanReferences MSBuild property as
    // [assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")].
    private const string ForceScanReferencesMetadataKey = "ErgosfareSourceGeneratorForceScanReferences";
    private const string AssemblyMetadataAttributeName = "AssemblyMetadataAttribute";
    private const string AssemblyMetadataAttributeNamespace = "System.Reflection";

    private static readonly string GeneratorVersion =
        typeof(ErgosfareRegistrationGenerator).Assembly.GetName().Version?.ToString() ?? "1.0.0";

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
                static (ctx, ct) => Transform(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        var availability = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var commandBuilder = compilation.GetTypeByMetadataName(CommandBuilderMetadataName);
            var queryBuilder = compilation.GetTypeByMetadataName(QueryBuilderMetadataName);
            var eventBuilder = compilation.GetTypeByMetadataName(EventBuilderMetadataName);
            var dispatchRoots = compilation.GetTypeByMetadataName(DispatchRootsMetadataName);

            return new ModuleBuilderAvailability(
                HasCompositionCatalog: compilation.GetTypeByMetadataName(CompositionCatalogMetadataName) is not null,
                HasCommandModuleBuilder: commandBuilder is not null,
                HasQueryModuleBuilder: queryBuilder is not null,
                HasEventModuleBuilder: eventBuilder is not null,
                CommandBuilderHasRegisterParticipants: HasRegisterParticipants(commandBuilder),
                QueryBuilderHasRegisterParticipants: HasRegisterParticipants(queryBuilder),
                EventBuilderHasRegisterParticipants: HasRegisterParticipants(eventBuilder),
                HasDispatchRoots: dispatchRoots is not null,
                DispatchRootsHasVoidPlans: dispatchRoots is not null && !dispatchRoots.GetMembers("AddVoidPlan").IsEmpty,
                DispatchRootsHasResultPlans: dispatchRoots is not null && !dispatchRoots.GetMembers("AddResultPlan").IsEmpty,
                DispatchRootsHasPlanFactories: dispatchRoots is not null && HasFactoryOverload(dispatchRoots),
                DispatchRootsHasProviderPlanFactories: dispatchRoots is not null
                    && HasProviderFactoryOverload(dispatchRoots)
                    && compilation.GetTypeByMetadataName(ServiceProviderExtensionsMetadataName) is not null,
                HasKeyedServiceExtensions: compilation.GetTypeByMetadataName(KeyedServiceExtensionsMetadataName) is not null,
                DispatchRootsHasStagedPlans: dispatchRoots is not null
                    && !dispatchRoots.GetMembers("AddStagedPlan").IsEmpty
                    && compilation.GetTypeByMetadataName(ServiceProviderExtensionsMetadataName) is not null,
                StagedPlansSupportDirectConstruction:
                    compilation.GetTypeByMetadataName(StagedVoidPlanMetadataName) is { } stagedVoidPlan
                    && !stagedVoidPlan.GetMembers("SupportsDirectConstruction").IsEmpty,
                HasDispatchSiteAttribute: compilation.GetTypeByMetadataName(DispatchSiteAttributeMetadataName) is not null,
                DispatchRootsHasFrozenCompositions: dispatchRoots is not null
                    && !dispatchRoots.GetMembers("AddFrozenComposition").IsEmpty);
        });

        // Reference scanning is default-on; consumers opt out per project through the
        // ErgosfareSourceGeneratorScanReferences MSBuild property (surfaced to the
        // generator as a build_property by the package's .props file).
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            !provider.GlobalOptions.TryGetValue(ScanReferencesBuildProperty, out var value)
            || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase));

        var referencedTypes = context.CompilationProvider
            .Combine(scanReferences)
            .Select(static (pair, ct) => pair.Right
                ? ScanReferencedAssemblies(pair.Left, ct)
                : ImmutableArray<RegistrableTypeModel>.Empty);

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
        // opaque shapes (RegisterFromAssembly, runtime-computed types) that make coverage
        // evidence incomplete.
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
                static (ctx, ct) => TransformDefaultResultAdapterSite(ctx, ct))
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
            static (spc, pair) => Execute(
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

    private static bool HasRegisterParticipants(INamedTypeSymbol? builder)
        => builder is not null && !builder.GetMembers("RegisterParticipants").IsEmpty;

    /// <summary>
    ///     Whether the referenced <c>GeneratedDispatchRoots</c> accepts a plan overload
    ///     with a direct-construction factory parameter — the surface the
    ///     <c>static () => new THandler()</c> emission requires.
    /// </summary>
    private static bool HasFactoryOverload(INamedTypeSymbol dispatchRoots)
    {
        foreach (var member in dispatchRoots.GetMembers("AddVoidPlan"))
        {
            if (member is IMethodSymbol { Parameters.Length: 1 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the referenced <c>GeneratedDispatchRoots</c> accepts a plan overload
    ///     with a provider-taking factory parameter
    ///     (<c>Func&lt;IServiceProvider, THandler&gt;</c>) — the surface the
    ///     dependency-injected construction emission requires. Recognized by delegate
    ///     arity: the parameterless factory overload's <c>Func&lt;THandler&gt;</c> has one
    ///     type argument, the provider-taking one has two.
    /// </summary>
    private static bool HasProviderFactoryOverload(INamedTypeSymbol dispatchRoots)
    {
        foreach (var member in dispatchRoots.GetMembers("AddVoidPlan"))
        {
            if (member is IMethodSymbol { Parameters.Length: 1 } method
                && method.Parameters[0].Type is INamedTypeSymbol { Arity: 2 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether generated code can construct the type with <c>new()</c> and doing so
    ///     is provably interchangeable with resolving its plain transient registration:
    ///     a concrete, non-generic class whose ONLY instance constructor is public and
    ///     parameterless (the container's greedy constructor selection would pick any
    ///     richer constructor, and it only considers public ones), with no <c>required</c>
    ///     members (a generated <c>new()</c> would fail compilation), implementing
    ///     neither <c>IDisposable</c> nor <c>IAsyncDisposable</c> (the container tracks
    ///     transient disposables; direct construction would not).
    /// </summary>
    private static bool IsDirectlyConstructible(INamedTypeSymbol symbol)
    {
        if (!HasDirectConstructionShape(symbol))
        {
            return false;
        }

        // Exactly one instance constructor, public and parameterless: with any richer
        // constructor present (records' synthesized copy constructor included), the
        // container's selection and `new()` can diverge — dropping dependencies the
        // container would have injected.
        return symbol.InstanceConstructors.Length == 1
               && symbol.InstanceConstructors[0] is { Parameters.IsEmpty: true, DeclaredAccessibility: Accessibility.Public };
    }

    /// <summary>
    ///     Shared base qualification of both construction factories: a concrete,
    ///     non-generic class implementing neither <c>IDisposable</c> nor
    ///     <c>IAsyncDisposable</c> (the container tracks transient disposables in the
    ///     resolving scope; direct construction would not), with no <c>required</c>
    ///     members anywhere in the hierarchy (an emitted <c>new</c> fails compilation
    ///     with CS9035, while the container activation the factory replaces ignores them).
    /// </summary>
    private static bool HasDirectConstructionShape(INamedTypeSymbol symbol)
    {
        if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.IsGenericType)
        {
            return false;
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface is { Name: "IDisposable" or "IAsyncDisposable", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } })
            {
                return false;
            }
        }

        for (var type = symbol; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Builds the provider-taking construction factory
    ///     (<c>static provider =&gt; new THandler(...)</c>) for a handler whose
    ///     construction is provably identical to container activation, or <c>null</c>
    ///     when the type does not qualify. The gate mirrors what
    ///     <c>Microsoft.Extensions.DependencyInjection</c> would do with the type's plain
    ///     transient registration: the container considers only public constructors, so a
    ///     type with exactly one public constructor leaves it no choice; every parameter
    ///     must be a plain service resolution (<c>GetRequiredService</c>) or a
    ///     <c>[FromKeyedServices]</c> one (<c>GetRequiredKeyedService</c>) from the very
    ///     provider container activation would resolve from. Anything that makes the
    ///     container's behavior content-dependent disqualifies: optional/default-valued
    ///     parameters (the container falls back to the default only when the service is
    ///     unregistered), multiple public constructors (greedy selection), <c>ref</c>-ish
    ///     or <c>params</c> parameters, <c>[ServiceKey]</c> injection, non-nameable
    ///     parameter types, and keys the emission cannot reproduce exactly.
    /// </summary>
    private static string? GetProviderConstructionExpression(
        INamedTypeSymbol symbol,
        string handlerTypeExpression,
        IAssemblySymbol? currentAssembly,
        out bool usesKeyedServices)
    {
        var construction = TryBuildConstructionExpression(
            symbol, handlerTypeExpression, currentAssembly, "provider", allowParameterless: false, out usesKeyedServices);

        return construction is null ? null : "static provider => " + construction;
    }

    /// <summary>
    ///     Builds the bare <c>new T(...)</c> expression for a participant whose
    ///     construction is provably identical to container activation (see
    ///     <see cref="GetProviderConstructionExpression"/> for the gate), resolving
    ///     constructor dependencies from the given provider identifier. The staged plans'
    ///     direct-construction emission consumes it with <c>serviceProvider</c>; the
    ///     provider factories wrap it in a lambda. Parameterless constructions are only
    ///     produced when asked for — the plan factories keep those on the cheaper
    ///     <c>Func&lt;THandler&gt;</c> shape.
    /// </summary>
    private static string? TryBuildConstructionExpression(
        INamedTypeSymbol symbol,
        string typeExpression,
        IAssemblySymbol? currentAssembly,
        string providerIdentifier,
        bool allowParameterless,
        out bool usesKeyedServices)
    {
        usesKeyedServices = false;

        if (!HasDirectConstructionShape(symbol))
        {
            return null;
        }

        IMethodSymbol? publicConstructor = null;

        foreach (var constructor in symbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public)
            {
                // Invisible to the container's constructor selection; irrelevant here too.
                continue;
            }

            if (publicConstructor is not null)
            {
                return null;
            }

            publicConstructor = constructor;
        }

        if (publicConstructor is null)
        {
            return null;
        }

        if (publicConstructor.Parameters.IsEmpty)
        {
            return allowParameterless ? "new " + typeExpression + "()" : null;
        }

        var arguments = new List<string>(publicConstructor.Parameters.Length);

        foreach (var parameter in publicConstructor.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.IsOptional || parameter.HasExplicitDefaultValue)
            {
                return null;
            }

            string? keyLiteral = null;

            foreach (var attribute in parameter.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeClass
                    || !IsDependencyInjectionNamespace(attributeClass.ContainingNamespace))
                {
                    continue;
                }

                switch (attributeClass.Name)
                {
                    case "FromKeyedServicesAttribute":
                        keyLiteral = GetServiceKeyLiteral(attribute, currentAssembly);

                        if (keyLiteral is null)
                        {
                            return null;
                        }

                        break;
                    case "ServiceKeyAttribute":
                        return null;
                }
            }

            if (parameter.Type is not INamedTypeSymbol parameterType
                || parameterType.IsRefLikeType
                || parameterType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                || !IsNameableClosedType(parameterType, currentAssembly))
            {
                return null;
            }

            var parameterTypeExpression = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            arguments.Add(keyLiteral is null
                ? "global::" + ServiceProviderExtensionsMetadataName + ".GetRequiredService<" + parameterTypeExpression + ">(" + providerIdentifier + ")"
                : "global::" + KeyedServiceExtensionsMetadataName + ".GetRequiredKeyedService<" + parameterTypeExpression + ">(" + providerIdentifier + ", " + keyLiteral + ")");

            usesKeyedServices |= keyLiteral is not null;
        }

        return "new " + typeExpression + "(" + string.Join(", ", arguments) + ")";
    }

    /// <summary>
    ///     Whether generated code in the current compilation can name the closed type in
    ///     a generic argument position: spellable names and public accessibility along the
    ///     whole containing chain (internal accepted only for the current compilation's
    ///     own types — referenced-assembly IVT grants are deliberately not modeled here),
    ///     recursively for every generic type argument.
    /// </summary>
    private static bool IsNameableClosedType(INamedTypeSymbol type, IAssemblySymbol? currentAssembly)
    {
        if (type.IsUnboundGenericType || !HasSpellableName(type))
        {
            return false;
        }

        for (var current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    break;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (currentAssembly is null
                        || !SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, currentAssembly))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        foreach (var argument in type.TypeArguments)
        {
            if (argument is not INamedTypeSymbol named || !IsNameableClosedType(named, currentAssembly))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDependencyInjectionNamespace(INamespaceSymbol? ns)
        => ns is
        {
            Name: "DependencyInjection",
            ContainingNamespace:
            {
                Name: "Extensions",
                ContainingNamespace: { Name: "Microsoft", ContainingNamespace.IsGlobalNamespace: true }
            }
        };

    /// <summary>
    ///     The C# literal reproducing a <c>[FromKeyedServices]</c> key exactly — the
    ///     container matches keys by boxed equality, so the emitted constant must carry
    ///     the same runtime type and value as the attribute's. Strings, chars, bools,
    ///     integral primitives, enums and <c>typeof</c> keys are reproducible; anything
    ///     else (null, floating-point, arrays) returns <c>null</c> and keeps the handler
    ///     on the container path.
    /// </summary>
    private static string? GetServiceKeyLiteral(AttributeData attribute, IAssemblySymbol? currentAssembly)
    {
        if (attribute.ConstructorArguments.Length != 1)
        {
            return null;
        }

        var key = attribute.ConstructorArguments[0];

        switch (key.Kind)
        {
            case TypedConstantKind.Primitive:
                return key.Value switch
                {
                    string s => SymbolDisplay.FormatLiteral(s, quote: true),
                    char c => SymbolDisplay.FormatLiteral(c, quote: true),
                    bool b => b ? "true" : "false",
                    int i => i.ToString(CultureInfo.InvariantCulture),
                    long l => l.ToString(CultureInfo.InvariantCulture) + "L",
                    sbyte v => "(sbyte)" + v.ToString(CultureInfo.InvariantCulture),
                    byte v => "(byte)" + v.ToString(CultureInfo.InvariantCulture),
                    short v => "(short)" + v.ToString(CultureInfo.InvariantCulture),
                    ushort v => "(ushort)" + v.ToString(CultureInfo.InvariantCulture),
                    uint v => v.ToString(CultureInfo.InvariantCulture) + "U",
                    ulong v => v.ToString(CultureInfo.InvariantCulture) + "UL",
                    _ => null,
                };
            case TypedConstantKind.Enum:
                return key.Type is INamedTypeSymbol enumType && IsNameableClosedType(enumType, currentAssembly)
                    ? "(" + enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")("
                      + Convert.ToString(key.Value, CultureInfo.InvariantCulture) + ")"
                    : null;
            case TypedConstantKind.Type:
                return key.Value is INamedTypeSymbol { IsUnboundGenericType: false } keyType
                       && IsNameableClosedType(keyType, currentAssembly)
                    ? "typeof(" + keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")"
                    : null;
            default:
                return null;
        }
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

        GetMarkers(symbol, out var isCommand, out var isQuery, out var isEvent);

        if (!isCommand && !isQuery && !isEvent)
        {
            return null;
        }

        if (IsExcludedFromDiscovery(symbol))
        {
            // Deliberately outside the closed world — but the reachability judgment must
            // know the zone exists, so the exclusion flows through as a shadow model
            // instead of vanishing.
            return CreateExcludedShadowModel(symbol, isCommand, isQuery, isEvent, referencedAssemblyName: null);
        }

        var isAccessible = IsAccessibleFromGeneratedCode(symbol);
        var descriptors = isAccessible ? BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && IsMessageShape(symbol, descriptors);
        var typeofExpression = BuildTypeofExpression(symbol);
        var dispatchResults = isDispatchable ? GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty;
        var hasIgnoredResultAdapter = false;
        var resultAdapter = isDispatchable
            ? GetResultAdapterModel(symbol, dispatchResults, isCommand, symbol.ContainingAssembly, out hasIgnoredResultAdapter)
            : null;

        var usesKeyedServices = false;
        var providerConstruction = isAccessible
            ? GetProviderConstructionExpression(symbol, typeofExpression, symbol.ContainingAssembly, out usesKeyedServices)
            : null;

        // Informational diagnostics apply to pipeline participants declared in source —
        // the only place the user can act on them.
        var hasMultipleCtors = !descriptors.IsEmpty && HasMultiplePublicInstanceConstructors(symbol);
        var hasFromServices = !descriptors.IsEmpty && HasFromServicesOnConstructor(symbol);

        var stagedKeyedServices = false;
        var stagedConstruction = isAccessible && !descriptors.IsEmpty
            ? TryBuildConstructionExpression(symbol, typeofExpression, symbol.ContainingAssembly,
                "serviceProvider", allowParameterless: true, out stagedKeyedServices)
            : null;

        return new RegistrableTypeModel
        {
            TypeofExpression = typeofExpression,
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = isAccessible,
            Location = isAccessible ? null : LocationInfo.From(symbol),
            Weight = GetWeight(symbol),
            GroupsExpression = GetGroupsExpression(symbol),
            GroupNames = GetGroupNames(symbol),
            Descriptors = descriptors,
            ReferencedAssemblyName = null,
            DiscoveryKeys = GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            IsMessageShape = isMessageShape,
            DispatchResults = dispatchResults,
            IsDirectlyConstructible = isAccessible && IsDirectlyConstructible(symbol),
            ProviderConstructionExpression = providerConstruction,
            ProviderConstructionUsesKeyedServices = usesKeyedServices,
            HasPipelineExclusion = HasPipelineExclusionAttribute(symbol),
            ExcludedInterceptorGroups = GetPipelineExclusionGroups(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            AssignableKeys = isMessageShape ? GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = isAccessible ? BuildContractShapes(symbol) : ImmutableArray<ContractShapeModel>.Empty,
            StagedConstructionExpression = stagedConstruction,
            StagedConstructionUsesKeyedServices = stagedKeyedServices,
            HasMultiplePublicConstructors = hasMultipleCtors,
            HasFromServicesConstructorParameter = hasFromServices,
            // Handler-bearing types keep their declaration location too: the
            // unreachable-handler diagnostics (ERGOSG007/008) anchor there; annotated
            // messages anchor ERGOSG011/012 and dispatchable ones ERGOSG013 the same way.
            InfoLocation = hasMultipleCtors || hasFromServices || !descriptors.IsEmpty
                           || resultAdapter is not null || isDispatchable
                ? LocationInfo.From(symbol)
                : null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = resultAdapter,
            HasIgnoredResultAdapter = hasIgnoredResultAdapter,
            MetadataSortKey = BuildMetadataName(symbol),
        };
    }

    /// <summary>
    ///     The reduced model of an <c>[ExcludeFromDiscovery]</c> type: only what the
    ///     reachability judgment's exclusion zone needs — the type's assignable chain when
    ///     it could be a runtime message instance, and its main-handler descriptor
    ///     messages when it carries handler contracts. Never emitted, never diagnosed.
    /// </summary>
    private static RegistrableTypeModel CreateExcludedShadowModel(
        INamedTypeSymbol symbol,
        bool isCommand,
        bool isQuery,
        bool isEvent,
        string? referencedAssemblyName)
    {
        var descriptors = BuildDescriptors(symbol);
        var isDispatchable = IsDispatchableMessage(symbol, descriptors);

        // Hidden from discovery, but still part of a pipeline: [ExcludeFromDiscovery]
        // keeps a type out of bulk registration, it does not stop a handler from being
        // written for it or someone registering it by hand. The frozen table therefore
        // describes these types too — as messages and as participants — and the consuming
        // container's own registrations decide whether the rows run. Emission names the
        // type, so unlike the judgment's exclusion zone this needs real accessibility.
        var isAccessible = IsAccessibleFromGeneratedCode(symbol);
        var isMessageShape = isAccessible && IsMessageShape(symbol, descriptors);

        return new RegistrableTypeModel
        {
            TypeofExpression = BuildTypeofExpression(symbol),
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = isAccessible,
            Location = null,
            Weight = GetWeight(symbol),
            GroupsExpression = GetGroupsExpression(symbol),
            GroupNames = GetGroupNames(symbol),
            Descriptors = descriptors,
            ReferencedAssemblyName = referencedAssemblyName,
            DiscoveryKeys = ImmutableArray<string>.Empty,
            IsDispatchableMessage = isDispatchable,
            IsMessageShape = isMessageShape,
            DispatchResults = ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = false,
            ProviderConstructionExpression = null,
            ProviderConstructionUsesKeyedServices = false,
            HasPipelineExclusion = HasPipelineExclusionAttribute(symbol),
            ExcludedInterceptorGroups = GetPipelineExclusionGroups(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            AssignableKeys = isMessageShape ? GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = ImmutableArray<ContractShapeModel>.Empty,
            StagedConstructionExpression = null,
            StagedConstructionUsesKeyedServices = false,
            HasMultiplePublicConstructors = false,
            HasFromServicesConstructorParameter = false,
            InfoLocation = null,
            IsExcludedFromDiscovery = true,
            ResultAdapter = null,
            HasIgnoredResultAdapter = false,
            MetadataSortKey = BuildMetadataName(symbol),
        };
    }

    private static bool HasMultiplePublicInstanceConstructors(INamedTypeSymbol symbol)
    {
        var count = 0;

        foreach (var constructor in symbol.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility == Accessibility.Public && ++count > 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFromServicesOnConstructor(INamedTypeSymbol symbol)
    {
        foreach (var constructor in symbol.InstanceConstructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                foreach (var attribute in parameter.GetAttributes())
                {
                    if (attribute.AttributeClass is { Name: "FromServicesAttribute" })
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the type can appear as a dispatched message instance: a concrete,
    ///     fully closed class or struct with no handler contracts. Only such types get
    ///     dispatch roots — abstract types and interfaces never carry a runtime message's
    ///     type, handlers are never dispatched, and open generics cannot be rooted.
    /// </summary>
    /// <summary>
    ///     Whether the type is a message shape a composition can be computed for: a
    ///     construct carrying no handler contracts of its own. Wider than
    ///     <see cref="IsDispatchableMessage"/> — an abstract base or an interface is never
    ///     dispatched itself, but it is what the frozen table's ancestor ladder lands on
    ///     when a message the generator never saw (a proxy, a type hidden from discovery)
    ///     is dispatched.
    /// </summary>
    /// <remarks>
    ///     A generic message definition is a shape too, and unlike
    ///     <see cref="IsDispatchableMessage"/> it needs no closed instantiation: the table
    ///     keys generic messages by their definition and the lookup normalizes a runtime
    ///     <c>Wrap&lt;int&gt;</c> to <c>Wrap&lt;&gt;</c>, so one entry serves every
    ///     instantiation. A generic containing type is still refused — see
    ///     <see cref="BuildDescriptors"/>.
    /// </remarks>
    private static bool IsMessageShape(INamedTypeSymbol symbol, ImmutableArray<DescriptorModel> descriptors)
    {
        if (descriptors.Length > 0)
        {
            return false;
        }

        if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
        {
            return false;
        }

        for (var current = symbol.ContainingType; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDispatchableMessage(INamedTypeSymbol symbol, ImmutableArray<DescriptorModel> descriptors)
    {
        if (symbol.IsAbstract || descriptors.Length > 0)
        {
            return false;
        }

        if (symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return false;
        }

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Collects the result roots of a dispatchable message from its closed marker
    ///     contracts: <c>ICommand&lt;T&gt;</c>/<c>IQuery&lt;T&gt;</c> feed the
    ///     result-executor path, <c>IStreamQuery&lt;T&gt;</c> the streaming path.
    /// </summary>
    private static ImmutableArray<DispatchResultModel> GetDispatchResults(INamedTypeSymbol symbol)
    {
        ImmutableArray<DispatchResultModel>.Builder? results = null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity != 1)
            {
                continue;
            }

            var isStream = false;

            switch (iface.Name)
            {
                case "ICommand" when IsInNamespace(iface, CommandMarkerNamespace):
                case "IQuery" when IsInNamespace(iface, QueryMarkerNamespace):
                    break;
                case "IStreamQuery" when IsInNamespace(iface, QueryMarkerNamespace):
                    isStream = true;
                    break;
                default:
                    continue;
            }

            var model = new DispatchResultModel(
                VerbatimTypeExpression(iface.TypeArguments[0]), isStream, iface.TypeArguments[0].IsValueType);

            results ??= ImmutableArray.CreateBuilder<DispatchResultModel>();

            if (!results.Contains(model))
            {
                results.Add(model);
            }
        }

        return results?.ToImmutable() ?? ImmutableArray<DispatchResultModel>.Empty;
    }

    /// <summary>
    ///     Projects a dispatchable message's <c>[ResultAdapter]</c> annotation, or
    ///     <c>null</c> when there is none, and reports whether the message carries
    ///     <c>[IgnoreResultAdapter]</c>. The runtime binding reads both attributes with
    ///     inheritance (<c>GetCustomAttribute</c>'s default), so the mirror walks the base
    ///     chain — the most derived annotation wins; the opt-out wins from any level.
    /// </summary>
    private static ResultAdapterModel? GetResultAdapterModel(
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
                if (attribute.AttributeClass is not { } attributeClass || !IsInNamespace(attributeClass, AttributeNamespace))
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
            // A typeof the model cannot resolve: nothing can ever bind — ERGOSG011
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

        var isBakeable = isInstantiable && IsNameableClosedType(adapterSymbol, currentAssembly);

        // The message's runtime-probed slots: every declared result (a stream probes its
        // enumerator), plus the Unit lane every command's void dispatch shape carries.
        var fitsDeclaredSlot = isCommand && ContainsAdapterSlot(adapterSlotsKey, UnitExpression);

        if (!fitsDeclaredSlot)
        {
            foreach (var dispatchResult in dispatchResults)
            {
                var slot = dispatchResult.IsStream
                    ? AsyncEnumeratorExpression + "<" + dispatchResult.ResultTypeExpression + ">"
                    : dispatchResult.ResultTypeExpression;

                if (ContainsAdapterSlot(adapterSlotsKey, slot))
                {
                    fitsDeclaredSlot = true;
                    break;
                }
            }
        }

        return new ResultAdapterModel(
            TypeofExpression: VerbatimTypeExpression(adapterSymbol),
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
    private static string BuildAdapterSlotsKey(INamedTypeSymbol adapterSymbol, string contractName)
    {
        StringBuilder? slots = null;

        foreach (var iface in adapterSymbol.AllInterfaces)
        {
            if (iface.Arity != 1 || iface.Name != contractName || !IsInNamespace(iface, CoreAbstractionsNamespace))
            {
                continue;
            }

            (slots ??= new StringBuilder()).Append(slots.Length == 0 ? string.Empty : "\x1f")
                .Append(VerbatimTypeExpression(iface.TypeArguments[0]));
        }

        return slots?.ToString() ?? string.Empty;
    }

    private static bool ContainsAdapterSlot(string slotsKey, string slot)
        => ("\x1f" + slotsKey + "\x1f").Contains("\x1f" + slot + "\x1f");

    private const string DependencyInjectionExtensionsNamespace = "Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection";

    /// <summary>
    ///     Projects one <c>UseDefaultResultAdapter(...)</c> callsite. A literal
    ///     <c>typeof</c> projects the adapter's served slots (closed) or slot patterns
    ///     (open definition); anything else — a variable, a conditional, an unresolvable
    ///     type — projects the opaque marker, which turns baking off compilation-wide.
    /// </summary>
    private static DefaultResultAdapterSiteModel? TransformDefaultResultAdapterSite(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method
            || method.Name != "UseDefaultResultAdapter"
            || !IsInNamespace(method.ContainingType, DependencyInjectionExtensionsNamespace))
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
            && HasSpellableName(definition)
            && IsAccessibleChain(definition, ctx.SemanticModel.Compilation.Assembly)
            && (isOpen || IsNameableClosedType(adapterSymbol, ctx.SemanticModel.Compilation.Assembly));

        string parameterNamesKey;
        var baseExpression = VerbatimTypeExpression(definition);

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
    private static bool IsAccessibleChain(INamedTypeSymbol symbol, IAssemblySymbol currentAssembly)
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
    ///     unbaked plans off served slots, and no ERGOSG013 judgment runs over facts the
    ///     mirror cannot see. Baking additionally requires <c>IsBakeable</c>.
    /// </summary>
    private static DefaultResultAdapterSiteModel? ReduceDefaultResultAdapter(
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
    ///     The ERGOSG013 predicate: a result-bearing message none of whose non-stream
    ///     result slots any adapter tier serves — not native, not the configured default.
    ///     Void and stream-only messages never qualify: they have no result value to
    ///     carry a failure in, so throwing is their inherent contract, not a misfit.
    /// </summary>
    private static bool HasUnservedResultSlots(
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

            if (TryGetNativeAdapterExpression(dispatchResult.ResultTypeExpression, out _)
                || TryBindDefaultAdapter(defaultAdapter, dispatchResult.ResultTypeExpression, out _, out _))
            {
                return false;
            }
        }

        return sawResultSlot;
    }

    /// <summary>
    ///     Binds the compilation's default adapter to a result slot: a closed adapter by
    ///     exact slot fit, an open definition by unifying the slot against its declared
    ///     carrier patterns and closing over the bound arguments — the compile-time
    ///     mirror of the runtime <c>DefaultResultAdapter</c>'s closing.
    /// </summary>
    private static bool TryBindDefaultAdapter(
        DefaultResultAdapterSiteModel defaultAdapter,
        string resultTypeExpression,
        out string? adapterTypeExpression,
        out bool materializes)
    {
        adapterTypeExpression = null;
        materializes = false;

        if (!defaultAdapter.IsOpenGeneric)
        {
            if (!ContainsAdapterSlot(defaultAdapter.AdapterSlotsKey, resultTypeExpression))
            {
                return false;
            }

            adapterTypeExpression = defaultAdapter.BaseTypeExpression;
            materializes = ContainsAdapterSlot(defaultAdapter.MaterializerSlotsKey, resultTypeExpression);
            return true;
        }

        var parameterNames = defaultAdapter.ParameterNamesKey.Split('\x1f');

        foreach (var pattern in SplitSlotsKey(defaultAdapter.AdapterSlotsKey))
        {
            var bindings = new string?[defaultAdapter.Arity];

            if (!TryMatchTypePattern(pattern, resultTypeExpression, parameterNames, bindings)
                || Array.IndexOf(bindings, null) >= 0)
            {
                continue;
            }

            adapterTypeExpression = defaultAdapter.BaseTypeExpression + "<" + string.Join(", ", bindings) + ">";

            foreach (var materializerPattern in SplitSlotsKey(defaultAdapter.MaterializerSlotsKey))
            {
                if (RenderTypePattern(materializerPattern, parameterNames, bindings) == resultTypeExpression)
                {
                    materializes = true;
                    break;
                }
            }

            return true;
        }

        return false;
    }

    private static string[] SplitSlotsKey(string slotsKey)
        => slotsKey.Length == 0 ? Array.Empty<string>() : slotsKey.Split('\x1f');

    /// <summary>
    ///     Structurally unifies a carrier pattern (a type expression whose bare
    ///     identifiers are the definition's type parameters) with a concrete slot
    ///     expression, binding parameters by position; a parameter met twice must bind
    ///     identically. Arrays, pointers and tuples are not unified through — their
    ///     patterns only match textually, mirroring the runtime unifier.
    /// </summary>
    private static bool TryMatchTypePattern(string pattern, string concrete, string[] parameterNames, string?[] bindings)
    {
        var parameterPosition = Array.IndexOf(parameterNames, pattern);

        if (parameterPosition >= 0)
        {
            if (bindings[parameterPosition] is { } bound)
            {
                return bound == concrete;
            }

            bindings[parameterPosition] = concrete;
            return true;
        }

        if (!TrySplitGenericExpression(pattern, out var patternName, out var patternArguments))
        {
            return pattern == concrete;
        }

        if (!TrySplitGenericExpression(concrete, out var concreteName, out var concreteArguments)
            || patternName != concreteName
            || patternArguments.Count != concreteArguments.Count)
        {
            return false;
        }

        for (var i = 0; i < patternArguments.Count; i++)
        {
            if (!TryMatchTypePattern(patternArguments[i], concreteArguments[i], parameterNames, bindings))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Substitutes bound parameters back into a pattern, reproducing the display format.</summary>
    private static string RenderTypePattern(string pattern, string[] parameterNames, string?[] bindings)
    {
        var parameterPosition = Array.IndexOf(parameterNames, pattern);

        if (parameterPosition >= 0)
        {
            return bindings[parameterPosition] ?? pattern;
        }

        if (!TrySplitGenericExpression(pattern, out var name, out var arguments))
        {
            return pattern;
        }

        var rendered = new StringBuilder(name).Append('<');

        for (var i = 0; i < arguments.Count; i++)
        {
            rendered.Append(i == 0 ? string.Empty : ", ").Append(RenderTypePattern(arguments[i], parameterNames, bindings));
        }

        return rendered.Append('>').ToString();
    }

    /// <summary>
    ///     Splits <c>Name&lt;A, B&lt;C&gt;&gt;</c> into the base name and its top-level
    ///     argument expressions; <c>false</c> for non-generic expressions (including
    ///     shapes the splitter does not model, such as tuples and arrays of generics —
    ///     those compare textually).
    /// </summary>
    private static bool TrySplitGenericExpression(string expression, out string name, out List<string> arguments)
    {
        name = expression;
        arguments = [];

        var open = expression.IndexOf('<');

        if (open < 0 || expression.Length == 0 || expression[expression.Length - 1] != '>')
        {
            return false;
        }

        name = expression.Substring(0, open);

        var depth = 0;
        var argumentStart = open + 1;

        for (var i = open; i < expression.Length; i++)
        {
            switch (expression[i])
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;

                    if (depth == 0 && i != expression.Length - 1)
                    {
                        // A '>' closing the outer list before the end: not a plain
                        // Name<...> shape (e.g. "X<T>.Nested") — compare textually.
                        return false;
                    }

                    break;
                case ',' when depth == 1:
                    arguments.Add(expression.Substring(argumentStart, i - argumentStart).Trim());
                    argumentStart = i + 1;
                    break;
            }
        }

        arguments.Add(expression.Substring(argumentStart, expression.Length - 1 - argumentStart).Trim());
        return true;
    }

    /// <summary>
    ///     Whether the type — or its containing assembly — opts out of discovery via
    ///     <c>[ExcludeFromDiscovery]</c>. Excluded types produce no registration and no
    ///     diagnostics: the exclusion is deliberate, unlike an inaccessible type.
    /// </summary>
    private static bool IsExcludedFromDiscovery(INamedTypeSymbol symbol)
        => HasExcludeFromDiscovery(symbol.GetAttributes())
           || HasExcludeFromDiscovery(symbol.ContainingAssembly.GetAttributes());

    /// <summary>
    ///     Whether the type declares <c>[ExcludeFromPipeline]</c>. The attribute shapes the
    ///     indirect interceptor stages at runtime; staged plans conservatively skip such
    ///     messages instead of modeling the exclusion.
    /// </summary>
    private static bool HasPipelineExclusionAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "ExcludeFromPipelineAttribute" } attributeClass
                && IsInNamespace(attributeClass, AttributeNamespace))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The group names a type's <c>[ExcludeFromPipeline]</c> names, or empty for the
    ///     parameterless (blanket) form and for types without the attribute. Mirrors
    ///     <c>MessageDescriptor</c>, which reads the attribute non-inherited.
    /// </summary>
    private static ImmutableArray<string> GetPipelineExclusionGroups(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: "ExcludeFromPipelineAttribute" } attributeClass
                || !IsInNamespace(attributeClass, AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var groups = ImmutableArray.CreateBuilder<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is string name)
                {
                    groups.Add(name);
                }
            }

            return groups.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    ///     The normalized type expressions of every base type and implemented interface —
    ///     the compile-time domain of the runtime's <c>IsAssignableTo</c> checks that admit
    ///     indirect (covariantly registered) interceptors into a message's pipeline.
    /// </summary>
    private static ImmutableArray<string> GetAssignableKeys(INamedTypeSymbol symbol)
    {
        var keys = ImmutableArray.CreateBuilder<string>();

        for (var baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            keys.Add(NormalizedTypeExpression(baseType));
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            keys.Add(NormalizedTypeExpression(iface));
        }

        return keys.ToImmutable();
    }

    /// <summary>
    ///     Collects the raw interceptor contracts the type implements — the undeduped
    ///     counterpart of <see cref="BuildDescriptors"/>'s interceptor walk, keeping the
    ///     async/sync and result-typed facts the staged-plan arm selection needs.
    /// </summary>
    private static ImmutableArray<ContractShapeModel> BuildContractShapes(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.Arity > 0)
            {
                return ImmutableArray<ContractShapeModel>.Empty;
            }
        }

        ImmutableArray<ContractShapeModel>.Builder? shapes = null;

        ReadExceptionFilter(symbol, out var exceptionFilterExpression, out var undecidableExceptionFilter);

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Arity is not (1 or 2) || !IsInNamespace(iface, HandlerContractNamespace))
            {
                continue;
            }

            var arguments = iface.TypeArguments;

            ContractShapeModel? shape = iface.Name switch
            {
                "IPreInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.PreInterceptor, IsAsync: false, IsResultTyped: false,
                    NormalizedTypeExpression(arguments[0]), null),
                "IAsyncPreInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.PreInterceptor, IsAsync: true, IsResultTyped: false,
                    NormalizedTypeExpression(arguments[0]), null),
                "IPostInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.PostInterceptor, IsAsync: false, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])),
                "IAsyncPostInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.PostInterceptor, IsAsync: true, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])),
                "IAsyncPostInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.PostInterceptor, IsAsync: true, IsResultTyped: false,
                    NormalizedTypeExpression(arguments[0]), null),
                "IExceptionInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: false, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1]),
                    exceptionFilterExpression, undecidableExceptionFilter),
                "IAsyncExceptionInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: true, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1]),
                    exceptionFilterExpression, undecidableExceptionFilter),
                "IAsyncExceptionInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: true, IsResultTyped: false,
                    NormalizedTypeExpression(arguments[0]), null,
                    exceptionFilterExpression, undecidableExceptionFilter),
                "IFinalInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.FinalInterceptor, IsAsync: false, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])),
                "IAsyncFinalInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.FinalInterceptor, IsAsync: true, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])),
                "IAsyncFinalInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.FinalInterceptor, IsAsync: true, IsResultTyped: false,
                    NormalizedTypeExpression(arguments[0]), null),
                _ => null,
            };

            if (shape is { } value)
            {
                (shapes ??= ImmutableArray.CreateBuilder<ContractShapeModel>()).Add(value);
            }
        }

        return shapes?.ToImmutable() ?? ImmutableArray<ContractShapeModel>.Empty;
    }

    /// <summary>
    ///     Reads the exception type an interceptor accepts off its
    ///     <c>IExceptionInterceptorFilter&lt;TException&gt;</c>, so the staged plan can
    ///     bake the runtime stage's filter probe in as an <c>is</c> test.
    /// </summary>
    /// <remarks>
    ///     Only the single-generic-filter shape is decidable. A type carrying the
    ///     non-generic filter without exactly one generic one has written its own
    ///     <c>Matches</c> (or has several to disambiguate by hand), and no compile-time test
    ///     reproduces it — the plan is disqualified instead of guessing, and the dispatch
    ///     asks the instance through the runtime stage.
    /// </remarks>
    private static void ReadExceptionFilter(INamedTypeSymbol symbol, out string? filterExpression, out bool undecidable)
    {
        filterExpression = null;
        undecidable = false;

        var carriesFilter = false;
        var genericFilterCount = 0;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Name != ExceptionFilterContractName || !IsInNamespace(iface, HandlerContractNamespace))
            {
                continue;
            }

            if (iface.Arity == 0)
            {
                carriesFilter = true;
                continue;
            }

            if (iface.Arity == 1)
            {
                genericFilterCount++;
                filterExpression = VerbatimTypeExpression(iface.TypeArguments[0]);
            }
        }

        if (!carriesFilter)
        {
            filterExpression = null;
            return;
        }

        if (genericFilterCount != 1)
        {
            filterExpression = null;
            undecidable = true;
        }
    }

    private static bool HasExcludeFromDiscovery(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is { Name: "ExcludeFromDiscoveryAttribute" } attributeClass
                && IsInNamespace(attributeClass, AttributeNamespace))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The type's effective discovery keys: its own <c>[DiscoveryKey]</c> keys when
    ///     declared, else its assembly's. Empty means default discovery (the implicit
    ///     empty-string key) — mirroring the runtime <c>Discovery</c> helper.
    /// </summary>
    private static ImmutableArray<string> GetDiscoveryKeys(INamedTypeSymbol symbol)
    {
        var keys = GetDeclaredDiscoveryKeys(symbol.GetAttributes());

        return keys.IsEmpty ? GetDeclaredDiscoveryKeys(symbol.ContainingAssembly.GetAttributes()) : keys;
    }

    private static ImmutableArray<string> GetDeclaredDiscoveryKeys(ImmutableArray<AttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { Name: "DiscoveryKeyAttribute" } attributeClass
                || !IsInNamespace(attributeClass, AttributeNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var values = attribute.ConstructorArguments[0].Values;

            if (values.IsDefaultOrEmpty)
            {
                return ImmutableArray<string>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is string key)
                {
                    builder.Add(key);
                }
            }

            return builder.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
    }

    /// <summary>
    ///     Determines which module markers (<c>ICommand</c>, <c>IQuery</c>, <c>IEvent</c>)
    ///     the type is assignable to. Handlers and interceptors inherit the marker through
    ///     their contract interfaces, so a single check covers messages, handlers and
    ///     interceptors alike.
    /// </summary>
    private static void GetMarkers(INamedTypeSymbol symbol, out bool isCommand, out bool isQuery, out bool isEvent)
    {
        isCommand = false;
        isQuery = false;
        isEvent = false;

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
    }

    /// <summary>
    ///     Discovers registrable marker types in the compilation's referenced assemblies —
    ///     the compile-time replacement for runtime scanning. Only assemblies that
    ///     themselves reference an Ergosfare assembly can contain marker types, so
    ///     everything else is skipped on a metadata-name check without realizing any of its
    ///     types; Ergosfare's own assemblies are excluded because their handler contract
    ///     interfaces inherit the module markers and must not be registered as user types.
    ///     A downstream assembly that deliberately lives under the reserved prefix can opt
    ///     back in per-assembly (see <see cref="HasForceScanReferencesOptIn"/>); the
    ///     library's own assemblies never do, so their contracts stay unregistered.
    /// </summary>
    private static ImmutableArray<RegistrableTypeModel> ScanReferencedAssemblies(
        Compilation compilation,
        CancellationToken ct)
    {
        ImmutableArray<RegistrableTypeModel>.Builder? results = null;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            if (!ReferencesErgosfare(assembly))
            {
                continue;
            }

            // The reserved-prefix exclusion is per-assembly opt-out-able in reverse: an
            // assembly under the prefix is skipped unless it explicitly force-opts-in.
            if (IsErgosfareAssemblyName(assembly.Name) && !HasForceScanReferencesOptIn(assembly))
            {
                continue;
            }

            // A library that opted out of discovery wholesale still shapes the judgment's
            // exclusion zone: its marker types flow through as shadow models only.
            var assemblyExcluded = HasExcludeFromDiscovery(assembly.GetAttributes());

            var givesAccess = assembly.GivesAccessTo(compilation.Assembly);

            CollectNamespaceTypes(assembly.GlobalNamespace, assembly.Name, givesAccess, assemblyExcluded, ref results, ct);
        }

        return results?.ToImmutable() ?? ImmutableArray<RegistrableTypeModel>.Empty;
    }

    /// <summary>
    ///     Whether the assembly name is Ergosfare's own (<c>Stella.Ergosfare</c> or a
    ///     dotted child of it).
    /// </summary>
    private static bool IsErgosfareAssemblyName(string name)
        => name.StartsWith(ErgosfareAssemblyNamePrefix, StringComparison.Ordinal)
           && (name.Length == ErgosfareAssemblyNamePrefix.Length
               || name[ErgosfareAssemblyNamePrefix.Length] == '.');

    /// <summary>
    ///     Whether the assembly force-opts back into reference scanning despite matching the
    ///     reserved <c>Stella.Ergosfare</c> name prefix. The opt-in is a per-assembly marker —
    ///     <c>[assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")]</c>,
    ///     surfaced from the same-named MSBuild property — so only assemblies that set it are
    ///     scanned. The library's own assemblies never declare it, which is what keeps their
    ///     marker-inheriting contract interfaces out of the generated registrations.
    /// </summary>
    private static bool HasForceScanReferencesOptIn(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: AssemblyMetadataAttributeName } attributeClass
                && IsInNamespace(attributeClass, AssemblyMetadataAttributeNamespace)
                && attribute.ConstructorArguments.Length == 2
                && attribute.ConstructorArguments[0].Value is string key
                && string.Equals(key, ForceScanReferencesMetadataKey, StringComparison.Ordinal)
                && attribute.ConstructorArguments[1].Value is string value
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the assembly's metadata records a reference to any Ergosfare assembly —
    ///     a pure name check over the assembly-reference table, no symbol realization.
    /// </summary>
    private static bool ReferencesErgosfare(IAssemblySymbol assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var reference in module.ReferencedAssemblies)
            {
                if (IsErgosfareAssemblyName(reference.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void CollectNamespaceTypes(
        INamespaceSymbol ns,
        string assemblyName,
        bool givesAccess,
        bool assemblyExcluded,
        ref ImmutableArray<RegistrableTypeModel>.Builder? results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol nestedNamespace)
            {
                CollectNamespaceTypes(nestedNamespace, assemblyName, givesAccess, assemblyExcluded, ref results, ct);
            }
            else if (member is INamedTypeSymbol type)
            {
                CollectTypeAndNested(type, assemblyName, givesAccess, assemblyExcluded, ref results);
            }
        }
    }

    private static void CollectTypeAndNested(
        INamedTypeSymbol type,
        string assemblyName,
        bool givesAccess,
        bool assemblyExcluded,
        ref ImmutableArray<RegistrableTypeModel>.Builder? results)
    {
        if (TryCreateReferencedModel(type, assemblyName, givesAccess, assemblyExcluded) is { } model)
        {
            (results ??= ImmutableArray.CreateBuilder<RegistrableTypeModel>()).Add(model);
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectTypeAndNested(nested, assemblyName, givesAccess, assemblyExcluded, ref results);
        }
    }

    /// <summary>
    ///     Projects a metadata type from a referenced assembly to its registration model,
    ///     or <c>null</c> when it carries no Ergosfare marker. Mirrors
    ///     <see cref="Transform"/>; descriptor computation is shared because both operate
    ///     on <see cref="INamedTypeSymbol"/>. Types the generated code cannot name —
    ///     internal without an <c>InternalsVisibleTo</c> grant, protected or private
    ///     nested, or compiler-mangled (file-local) — flow through as inaccessible and
    ///     surface as ERGOSG002.
    /// </summary>
    private static RegistrableTypeModel? TryCreateReferencedModel(
        INamedTypeSymbol symbol,
        string assemblyName,
        bool givesAccess,
        bool assemblyExcluded)
    {
        if (symbol.IsStatic || symbol.IsImplicitlyDeclared)
        {
            return null;
        }

        GetMarkers(symbol, out var isCommand, out var isQuery, out var isEvent);

        if (!isCommand && !isQuery && !isEvent)
        {
            return null;
        }

        if (assemblyExcluded || IsExcludedFromDiscovery(symbol))
        {
            // Same shadow posture as source-declared exclusions; see Transform.
            return CreateExcludedShadowModel(symbol, isCommand, isQuery, isEvent, assemblyName);
        }

        var isAccessible = IsVisibleToCompilation(symbol, givesAccess) && HasSpellableName(symbol);
        var descriptors = isAccessible ? BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && IsDispatchableMessage(symbol, descriptors);
        var isMessageShape = isAccessible && IsMessageShape(symbol, descriptors);
        var typeofExpression = BuildTypeofExpression(symbol);
        var dispatchResults = isDispatchable ? GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty;

        // Referenced adapters get no current-assembly grant either: baking qualifies only
        // over fully public adapter types. ERGOSG011/012 never fire here (the annotations
        // were judged where the message was compiled); the model only feeds the plan binding.
        var referencedHasIgnore = false;
        var resultAdapter = isDispatchable
            ? GetResultAdapterModel(symbol, dispatchResults, isCommand, currentAssembly: null, out referencedHasIgnore)
            : null;

        // Referenced handlers get no current-assembly grant: their construction factory
        // qualifies only over fully public parameter types (IVT grants are not modeled).
        var usesKeyedServices = false;
        var providerConstruction = isAccessible
            ? GetProviderConstructionExpression(symbol, typeofExpression, currentAssembly: null, out usesKeyedServices)
            : null;

        var referencedStagedKeyedServices = false;
        var referencedStagedConstruction = isAccessible && !descriptors.IsEmpty
            ? TryBuildConstructionExpression(symbol, typeofExpression, currentAssembly: null,
                "serviceProvider", allowParameterless: true, out referencedStagedKeyedServices)
            : null;

        return new RegistrableTypeModel
        {
            TypeofExpression = typeofExpression,
            DisplayName = symbol.ToDisplayString(),
            IsCommand = isCommand,
            IsQuery = isQuery,
            IsEvent = isEvent,
            IsAccessible = isAccessible,
            Location = null,
            Weight = GetWeight(symbol),
            GroupsExpression = GetGroupsExpression(symbol),
            GroupNames = GetGroupNames(symbol),
            Descriptors = descriptors,
            ReferencedAssemblyName = assemblyName,
            DiscoveryKeys = GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            IsMessageShape = isMessageShape,
            DispatchResults = dispatchResults,
            IsDirectlyConstructible = isAccessible && IsDirectlyConstructible(symbol),
            ProviderConstructionExpression = providerConstruction,
            ProviderConstructionUsesKeyedServices = usesKeyedServices,
            HasPipelineExclusion = HasPipelineExclusionAttribute(symbol),
            ExcludedInterceptorGroups = GetPipelineExclusionGroups(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            AssignableKeys = isMessageShape ? GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = isAccessible ? BuildContractShapes(symbol) : ImmutableArray<ContractShapeModel>.Empty,
            StagedConstructionExpression = referencedStagedConstruction,
            StagedConstructionUsesKeyedServices = referencedStagedKeyedServices,
            HasMultiplePublicConstructors = false,
            HasFromServicesConstructorParameter = false,
            InfoLocation = null,
            IsExcludedFromDiscovery = false,
            ResultAdapter = resultAdapter,
            HasIgnoredResultAdapter = referencedHasIgnore,
            MetadataSortKey = BuildMetadataName(symbol),
        };
    }

    /// <summary>
    ///     Whether generated code in the current compilation can name a type declared in a
    ///     referenced assembly: every level of the containing-type chain must be public, or
    ///     internal with the assembly granting this compilation
    ///     <c>InternalsVisibleTo</c> access.
    /// </summary>
    private static bool IsVisibleToCompilation(INamedTypeSymbol symbol, bool givesAccess)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                    break;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    if (!givesAccess)
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
    ///     Whether the type's full containing chain uses names spellable in C# source.
    ///     File-local types survive into metadata as internal types with compiler-mangled
    ///     names (<c>&lt;File&gt;F...__Type</c>) that a <c>typeof</c> cannot express.
    /// </summary>
    private static bool HasSpellableName(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (!SyntaxFacts.IsValidIdentifier(current.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> sourceModels,
        ModuleBuilderAvailability availability,
        ImmutableArray<RegistrableTypeModel> referencedModels,
        ImmutableArray<DispatchSiteModel> dispatchSites,
        ImmutableArray<RegistrationSiteModel> registrationSites,
        DispatchManifestScanResult referencedSites,
        JudgmentInputs judgmentInputs,
        ImmutableArray<DefaultResultAdapterSiteModel> defaultResultAdapterSites,
        ImmutableArray<PluginInvocationModel> pluginInvocations)
    {
        var seen = new HashSet<string>();
        var types = new List<RegistrableTypeModel>();
        var excludedShadows = new List<RegistrableTypeModel>();
        var defaultResultAdapter = ReduceDefaultResultAdapter(defaultResultAdapterSites);

        // Source-declared types first: on a (pathological) full-name collision with a
        // referenced type, typeof in the generated file binds to the source declaration.
        AddModels(context, sourceModels, seen, types, excludedShadows, defaultResultAdapter);
        AddModels(context, referencedModels, seen, types, excludedShadows, defaultResultAdapter);

        // Reachability verdicts and the opt-in handler trim; the returned list is what
        // emission proceeds with.
        types = ApplyDispatchJudgment(
            context, types, excludedShadows, dispatchSites, registrationSites, referencedSites, judgmentInputs);

        // The manifest must be emitted even from a compilation that declares no
        // registrable type at all — a callsite-only library's sites would otherwise be
        // invisible to the composition root, and a siteless assembly's marker is exactly
        // what distinguishes "dispatches nothing" from "unknown".
        var emitManifest = availability.HasDispatchSiteAttribute;

        if (types.Count == 0 && !emitManifest)
        {
            return;
        }

        // Deterministic output regardless of declaration/discovery order.
        types.Sort(static (x, y) => string.CompareOrdinal(x.TypeofExpression, y.TypeofExpression));

        var voidPlans = availability.DispatchRootsHasVoidPlans
            ? ComputeVoidPlans(types)
            : (IReadOnlyList<VoidPlanModel>)Array.Empty<VoidPlanModel>();

        var resultPlans = availability.DispatchRootsHasResultPlans
            ? ComputeResultPlans(types)
            : (IReadOnlyList<ResultPlanModel>)Array.Empty<ResultPlanModel>();

        var stagedPlans = availability.DispatchRootsHasStagedPlans
            ? ComputeStagedPlans(types, availability.HasKeyedServiceExtensions,
                defaultResultAdapter is { IsBakeable: true } ? defaultResultAdapter : null,
                pluginInvocations)
            : (IReadOnlyList<StagedPlanModel>)Array.Empty<StagedPlanModel>();

        // A message a plugin pulled into the staged family leaves the single-handler one:
        // the executor checks staged plans first, and two plans for one message would leave
        // the handler plan permanently dead while still costing its emission and its
        // registration-time validation. Without a plugin the two families are disjoint by
        // construction, so this filters nothing.
        if (!pluginInvocations.IsEmpty && stagedPlans.Count > 0)
        {
            var stagedMessages = new HashSet<string>(StringComparer.Ordinal);

            foreach (var plan in stagedPlans)
            {
                stagedMessages.Add(plan.MessageTypeExpression);
            }

            voidPlans = WithoutMessages(voidPlans, stagedMessages, static plan => plan.MessageTypeExpression);
            resultPlans = WithoutMessages(resultPlans, stagedMessages, static plan => plan.MessageTypeExpression);
        }

        var frozenCompositions = availability.DispatchRootsHasFrozenCompositions
            ? ComputeFrozenCompositions(types, excludedShadows)
            : (IReadOnlyList<FrozenCompositionModel>)Array.Empty<FrozenCompositionModel>();

        var source = RegistrationEmitter.Emit(types, availability, voidPlans, resultPlans, stagedPlans,
            frozenCompositions,
            emitManifest ? dispatchSites : ImmutableArray<DispatchSiteModel>.Empty,
            emitManifest ? registrationSites : ImmutableArray<RegistrationSiteModel>.Empty,
            emitManifest, GeneratorVersion);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    /// <summary>The plans whose message is not in the given set, without copying when none is.</summary>
    private static IReadOnlyList<TPlan> WithoutMessages<TPlan>(
        IReadOnlyList<TPlan> plans, HashSet<string> excluded, Func<TPlan, string> messageOf)
    {
        List<TPlan>? kept = null;

        for (var i = 0; i < plans.Count; i++)
        {
            if (!excluded.Contains(messageOf(plans[i])))
            {
                kept?.Add(plans[i]);
                continue;
            }

            if (kept is null)
            {
                kept = new List<TPlan>(plans.Count);

                for (var j = 0; j < i; j++)
                {
                    kept.Add(plans[j]);
                }
            }
        }

        return kept ?? plans;
    }

    /// <summary>
    ///     Computes the frozen pipeline compositions: for every dispatchable message, the
    ///     full six-stage participant table in the runtime shape-builder's exact execution
    ///     order — direct/indirect split at the seam the runtime descriptor split uses
    ///     (declared message equality vs assignability), each segment sorted
    ///     weight-descending then ordinal CLR <c>FullName</c>, group labels baked per row.
    ///     The table is the dispatch authority, so it carries what the registry used to.
    ///     Keyed participants get rows like any other — which of them an application runs
    ///     is settled by what it registered, and the consuming catalog narrows the table
    ///     to exactly that. A message's <c>[ExcludeFromPipeline]</c> is resolved here:
    ///     the blanket form drops every covariantly matched interceptor, the group-scoped
    ///     form drops the covariant interceptors carrying an excluded group, and neither
    ///     touches directly registered interceptors or main handlers.
    /// </summary>
    /// <remarks>
    ///     A participant generated code cannot name (inaccessible) contributes no row:
    ///     no registration surface can reference it either, so its absence from the table
    ///     is the same absence the container already sees. An exact comparator tie —
    ///     equal weight and equal metadata name, i.e. the same type reached twice — is
    ///     broken by discovery order, which is deterministic and, the participants being
    ///     identical, unobservable.
    /// </remarks>
    private static List<FrozenCompositionModel> ComputeFrozenCompositions(
        List<RegistrableTypeModel> types, List<RegistrableTypeModel> excludedShadows)
    {
        var compositions = new List<FrozenCompositionModel>();
        var rows = new List<(uint Weight, string SortKey, FrozenParticipantModel Row)>?[10];

        // Messages hidden from discovery still get entries: [ExcludeFromDiscovery] keeps a
        // type out of bulk registration, it does not stop a handler from being written for
        // it — and without an entry such a message (and every subtype resolving through it)
        // would have no pipeline at all. Participants are widened the same way: a hidden
        // interceptor is registered by hand, and the consuming catalog admits its row only
        // for the container that did register it.
        foreach (var message in Enumerate(types, excludedShadows))
        {
            if (!message.IsMessageShape)
            {
                continue;
            }

            var messageKey = DefinitionKey(message.TypeofExpression);
            Array.Clear(rows, 0, rows.Length);

            foreach (var candidate in Enumerate(types, excludedShadows))
            {
                if (candidate.Descriptors.IsEmpty || !candidate.IsAccessible)
                {
                    continue;
                }

                foreach (var descriptor in candidate.Descriptors)
                {
                    var declaredKey = DefinitionKey(descriptor.MessageTypeExpression);
                    var direct = declaredKey == messageKey;

                    if (!direct && !ContainsAssignableKey(message, declaredKey))
                    {
                        continue;
                    }

                    if (!direct
                        && descriptor.Kind != DescriptorKind.MainHandler
                        && IsExcludedFromPipeline(message, candidate))
                    {
                        continue;
                    }

                    var segment = (int)descriptor.Kind * 2 + (direct ? 0 : 1);
                    (rows[segment] ??= []).Add((
                        candidate.Weight,
                        candidate.MetadataSortKey,
                        new FrozenParticipantModel(
                            candidate.TypeofExpression, candidate.GroupsExpression)));
                }
            }

            var segments = new ImmutableArray<FrozenParticipantModel>[10];
            var isEmpty = true;

            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] is not { Count: > 0 } segmentRows)
                {
                    segments[i] = ImmutableArray<FrozenParticipantModel>.Empty;
                    continue;
                }

                isEmpty = false;
                segmentRows.Sort(static (x, y) =>
                {
                    var byWeight = y.Weight.CompareTo(x.Weight);
                    return byWeight != 0 ? byWeight : string.CompareOrdinal(x.SortKey, y.SortKey);
                });

                var builder = ImmutableArray.CreateBuilder<FrozenParticipantModel>(segmentRows.Count);

                foreach (var row in segmentRows)
                {
                    builder.Add(row.Row);
                }

                segments[i] = builder.MoveToImmutable();
            }

            if (isEmpty)
            {
                continue;
            }

            compositions.Add(new FrozenCompositionModel(
                message.TypeofExpression,
                segments[0], segments[1], segments[2], segments[3], segments[4],
                segments[5], segments[6], segments[7], segments[8], segments[9]));
        }

        return compositions;
    }

    /// <summary>Both model lists in order, without materializing a combined one.</summary>
    private static IEnumerable<RegistrableTypeModel> Enumerate(
        List<RegistrableTypeModel> types, List<RegistrableTypeModel> excludedShadows)
    {
        foreach (var type in types)
        {
            yield return type;
        }

        foreach (var shadow in excludedShadows)
        {
            yield return shadow;
        }
    }

    /// <summary>
    ///     Whether the message's <c>[ExcludeFromPipeline]</c> keeps a covariantly matched
    ///     interceptor out of its pipeline: the parameterless form excludes every one, the
    ///     group-scoped form only those carrying a named group. Mirrors the runtime
    ///     shape-builder's <c>PrepareIndirect</c>, including its treatment of an
    ///     interceptor without <c>[Group]</c> as carrying the default group alone.
    /// </summary>
    private static bool IsExcludedFromPipeline(RegistrableTypeModel message, RegistrableTypeModel interceptor)
    {
        if (!message.HasPipelineExclusion)
        {
            return false;
        }

        if (message.ExcludedInterceptorGroups.IsEmpty)
        {
            return true;
        }

        foreach (var excluded in message.ExcludedInterceptorGroups)
        {
            if (interceptor.GroupNames.IsEmpty)
            {
                if (excluded == DefaultGroupName)
                {
                    return true;
                }

                continue;
            }

            foreach (var group in interceptor.GroupNames)
            {
                if (string.Equals(group, excluded, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsAssignableKey(RegistrableTypeModel message, string declaredKey)
    {
        foreach (var assignableKey in message.AssignableKeys)
        {
            if (DefinitionKey(assignableKey) == declaredKey)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Computes the staged pipeline plans: a dispatchable command (void) or
    ///     command/query (single closed result) qualifies when its sole discovered handler
    ///     is the matching async contract AND at least one discovered interceptor
    ///     participates in its pipeline AND every part of that pipeline could be modeled
    ///     exactly — the composition (membership and order) replicates the runtime
    ///     shape-builder, and every call's pattern-match arm is decidable at compile time.
    ///     Anything unmodelable disqualifies the message rather than risking divergence;
    ///     the runtime gate then simply never sees a staged plan for it. The plan stays
    ///     advisory regardless: the hosting executor validates it against the container's
    ///     selected frozen composition.
    /// </summary>
    private static List<StagedPlanModel> ComputeStagedPlans(
        List<RegistrableTypeModel> types,
        bool hasKeyedServiceExtensions,
        DefaultResultAdapterSiteModel? defaultResultAdapter,
        ImmutableArray<PluginInvocationModel> pluginInvocations)
    {
        CollectPipelineFacts(types, out var handlerCounts, out var soleHandlers, out _);

        var plans = new List<StagedPlanModel>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || type.HasPipelineExclusion)
            {
                continue;
            }

            string? resultTypeExpression = null;
            var resultIsValueType = false;

            if (type.IsCommand && type.DispatchResults.Length == 0)
            {
                // Void pipeline.
            }
            else if ((type.IsCommand || type.IsQuery)
                     && type.DispatchResults.Length == 1
                     && !type.DispatchResults[0].IsStream)
            {
                resultTypeExpression = type.DispatchResults[0].ResultTypeExpression;
                resultIsValueType = type.DispatchResults[0].ResultIsValueType;
            }
            else
            {
                continue;
            }

            // The sole handler gate mirrors the single-handler plans, minus the
            // interceptor suppression (interceptors are the whole point here).
            if (!handlerCounts.TryGetValue(type.TypeofExpression, out var count) || count != 1)
            {
                continue;
            }

            if (HasCovariantMainHandler(type, handlerCounts))
            {
                continue;
            }

            var (handler, handlerDescriptor) = soleHandlers[type.TypeofExpression];

            if (!handler.IsAccessible || !handler.DiscoveryKeys.IsEmpty || handler.GroupsExpression is not null)
            {
                continue;
            }

            var expectedHandlerResult = resultTypeExpression is null
                ? ValueTaskExpression
                : ValueTaskExpression + "<" + resultTypeExpression + ">";

            if (handlerDescriptor.ResultTypeExpression != expectedHandlerResult
                || handlerDescriptor.MessageTypeExpression != type.TypeofExpression)
            {
                continue;
            }

            if (!TryAssembleStagedStages(type, types, resultTypeExpression, resultIsValueType, hasKeyedServiceExtensions,
                    out var pre, out var post, out var exceptionCalls, out var finalCalls))
            {
                continue;
            }

            var pluginCalls = SelectPluginCalls(pluginInvocations, type, resultTypeExpression is null);

            if (pre.Length + post.Length + exceptionCalls.Length + finalCalls.Length == 0
                && pluginCalls.IsEmpty)
            {
                // No interceptors and no plugin: the single-handler plans already cover this
                // shape. A plugin is what pulls an interceptorless pipeline in here — its
                // observer has to be emitted into both plan families, and a plan body is the
                // only place a call can live. The body collapses accordingly: with no pre
                // chain, the pipeline start and the pre-handler boundary are the same point,
                // as are the post-handler and after-post ones.
                continue;
            }

            // The runtime binding's compile-time mirror: the opt-out suppresses every
            // tier; else the annotation when it fits the slot exactly, else the native
            // carriers, else the compilation's discovered default adapter, else nothing.
            // A fitting annotation the plan cannot bake (inaccessible or uninstantiable
            // adapter) disqualifies the plan — the runtime mirror serves the pipeline
            // instead. A default the discovery could not model (opaque callsite,
            // disagreeing sites, unbakeable type) bakes nothing: a slot it binds at
            // runtime then fails the hosting executor's adapter-identity gate and stays
            // on the strategy.
            var adapterKind = StagedResultAdapterKind.None;
            string? adapterTypeExpression = null;
            var adapterMaterializes = false;

            if (type.HasIgnoredResultAdapter)
            {
                // Classic emission; the runtime binding resolves to null for every tier.
            }
            else if (resultTypeExpression is not null)
            {
                if (type.ResultAdapter is { } annotation && annotation.Fits(resultTypeExpression))
                {
                    if (!annotation.IsBakeable)
                    {
                        continue;
                    }

                    adapterKind = StagedResultAdapterKind.Custom;
                    adapterTypeExpression = annotation.TypeofExpression;
                    adapterMaterializes = annotation.Materializes(resultTypeExpression);
                }
                else if (TryGetNativeAdapterExpression(resultTypeExpression, out adapterTypeExpression))
                {
                    adapterKind = StagedResultAdapterKind.Native;
                    adapterMaterializes = true;
                }
                else if (defaultResultAdapter is not null
                         && TryBindDefaultAdapter(defaultResultAdapter, resultTypeExpression,
                             out adapterTypeExpression, out adapterMaterializes))
                {
                    adapterKind = StagedResultAdapterKind.Custom;
                }
            }
            else if (type.ResultAdapter is { } voidAnnotation && voidAnnotation.Fits(UnitExpression))
            {
                // A Unit-fitting annotation binds the void lane at runtime; void plans do
                // not model adapters, so the plan is disqualified rather than diverging.
                continue;
            }
            else if (!type.HasIgnoredResultAdapter
                     && defaultResultAdapter is not null
                     && TryBindDefaultAdapter(defaultResultAdapter, UnitExpression, out _, out _))
            {
                // A Unit-serving default binds every void lane at runtime; same posture.
                continue;
            }

            plans.Add(new StagedPlanModel(
                type.TypeofExpression,
                resultTypeExpression,
                resultIsValueType,
                handler.TypeofExpression,
                GatedConstructionExpression(handler, hasKeyedServiceExtensions),
                pre, post, exceptionCalls, finalCalls,
                adapterKind, adapterTypeExpression, adapterMaterializes,
                pluginCalls));
        }

        return plans;
    }

    /// <summary>
    ///     The plugin methods emitted into one plan: those whose declared shape matches the
    ///     pipeline's, whose family filter admits the message's module, whose discovery-key
    ///     filter admits the message's keys, and whose generic constraints the message
    ///     satisfies. A method failing any of them contributes nothing to this plan — no
    ///     call, no runtime check — which is the design's point about a constraint being the
    ///     filter.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The order is ordinal by service type then method name. Weight-by-registration
    ///         order is a property of the consumer's fluent chain, which this slice does not
    ///         read; a stable arbitrary order is preferable to an unstable one, and pinning it
    ///         here keeps the emitted source deterministic.
    ///     </para>
    ///     <para>
    ///         Events never reach this path: the staged family covers void commands and
    ///         single-result commands and queries, so a plugin filtered to the event module
    ///         alone currently selects nothing.
    ///     </para>
    /// </remarks>
    private static ImmutableArray<PluginInvocationModel> SelectPluginCalls(
        ImmutableArray<PluginInvocationModel> invocations,
        RegistrableTypeModel message,
        bool isVoidPipeline)
    {
        if (invocations.IsEmpty)
        {
            return ImmutableArray<PluginInvocationModel>.Empty;
        }

        var shape = isVoidPipeline ? PluginPipelineShape.Void : PluginPipelineShape.Result;

        // The method's own arity is what emission closes over: one type parameter for the
        // resultless shape, two for the result-bearing one. Anything else is a declaration
        // this emission cannot write a call for.
        var expectedArity = isVoidPipeline ? 1 : 2;

        ImmutableArray<PluginInvocationModel>.Builder? selected = null;

        foreach (var invocation in invocations)
        {
            if (invocation.Shape != shape
                || invocation.Arity != expectedArity
                || invocation.Constraints.IsUnmodelable
                || !MatchesModule(invocation.Modules, message)
                || !MatchesDiscoveryKeys(invocation.Keys, message.DiscoveryKeys)
                || !SatisfiesConstraints(invocation.Constraints, message))
            {
                continue;
            }

            (selected ??= ImmutableArray.CreateBuilder<PluginInvocationModel>()).Add(invocation);
        }

        if (selected is null)
        {
            return ImmutableArray<PluginInvocationModel>.Empty;
        }

        selected.Sort(static (x, y) =>
        {
            var byType = string.CompareOrdinal(x.ServiceTypeExpression, y.ServiceTypeExpression);
            return byType != 0 ? byType : string.CompareOrdinal(x.MethodName, y.MethodName);
        });

        return selected.ToImmutable();
    }

    private static bool MatchesModule(PluginModule modules, RegistrableTypeModel message)
        => (message.IsCommand && (modules & PluginModule.Command) != 0)
           || (message.IsQuery && (modules & PluginModule.Query) != 0)
           || (message.IsEvent && (modules & PluginModule.Event) != 0);

    /// <summary>
    ///     The key filter. An unwritten one selects the default key alone — the same set
    ///     <c>RegisterGenerated()</c> without a pattern selects. A keyed message was opted
    ///     out of default discovery by its author, and a plugin the consumer installed
    ///     without naming a key should not quietly opt it back in.
    /// </summary>
    private static bool MatchesDiscoveryKeys(ImmutableArray<string> filter, ImmutableArray<string> declared)
    {
        if (filter.IsDefaultOrEmpty)
        {
            return declared.IsDefaultOrEmpty;
        }

        foreach (var key in filter)
        {
            if (declared.IsDefaultOrEmpty)
            {
                if (key.Length == 0)
                {
                    return true;
                }

                continue;
            }

            foreach (var candidate in declared)
            {
                if (string.Equals(key, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether the message satisfies the method's message-parameter constraints, decided
    ///     against the same assignable chain the covariant interceptor match uses.
    /// </summary>
    private static bool SatisfiesConstraints(PluginConstraintModel constraints, RegistrableTypeModel message)
    {
        if (constraints.RequiresReferenceType && message.IsValueType)
        {
            return false;
        }

        if (constraints.RequiresValueType && !message.IsValueType)
        {
            return false;
        }

        foreach (var required in constraints.MessageTypes)
        {
            if (required == message.TypeofExpression)
            {
                continue;
            }

            if (!message.AssignableKeys.Contains(required))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     The built-in adapter expression of a native carrier result slot —
    ///     <c>ResultExceptionAdapter</c> for <c>Result</c>, its closed generic twin for
    ///     <c>Result&lt;T&gt;</c> — or <c>false</c> for every other result type.
    /// </summary>
    private static bool TryGetNativeAdapterExpression(string resultTypeExpression, out string? adapterTypeExpression)
    {
        if (resultTypeExpression == NativeResultExpression)
        {
            adapterTypeExpression = NativeResultAdapterExpression;
            return true;
        }

        if (resultTypeExpression.Length > NativeResultExpression.Length + 2
            && resultTypeExpression.StartsWith(NativeResultExpression + "<", StringComparison.Ordinal)
            && resultTypeExpression[resultTypeExpression.Length - 1] == '>')
        {
            adapterTypeExpression = NativeResultAdapterExpression
                + resultTypeExpression.Substring(NativeResultExpression.Length);
            return true;
        }

        adapterTypeExpression = null;
        return false;
    }

    /// <summary>
    ///     Assembles the four staged interceptor stages for a message in the runtime
    ///     shape-builder's exact execution order, or fails when any participant cannot be
    ///     modeled: grouped/keyed/inaccessible/nested participants, a participant matching
    ///     through more than one deduped contract registration, a participant with no
    ///     variance-resolvable arm (the runtime would throw for it), or a reference-typed
    ///     pipeline result with an inexactly-typed contract (runtime result variance the
    ///     string model cannot verify).
    /// </summary>
    /// <summary>
    ///     The participant's staged construction expression, or <c>null</c> when keyed
    ///     resolutions are needed but the keyed-service extensions are not resolvable in
    ///     the consuming compilation.
    /// </summary>
    private static string? GatedConstructionExpression(RegistrableTypeModel participant, bool hasKeyedServiceExtensions)
        => participant.StagedConstructionUsesKeyedServices && !hasKeyedServiceExtensions
            ? null
            : participant.StagedConstructionExpression;

    private static bool TryAssembleStagedStages(
        RegistrableTypeModel message,
        List<RegistrableTypeModel> types,
        string? resultTypeExpression,
        bool resultIsValueType,
        bool hasKeyedServiceExtensions,
        out ImmutableArray<StagedCallModel> preCalls,
        out ImmutableArray<StagedCallModel> postCalls,
        out ImmutableArray<StagedCallModel> exceptionCalls,
        out ImmutableArray<StagedCallModel> finalCalls)
    {
        preCalls = postCalls = exceptionCalls = finalCalls = ImmutableArray<StagedCallModel>.Empty;

        // The pipeline result the arms match against: the declared result for result
        // pipelines, Unit for void ones — a reference type, so a void pipeline is on the
        // variance-bearing side of the checks below just like a class-typed result.
        var pipelineResultExpression = resultTypeExpression ?? UnitExpression;
        var pipelineResultIsValueType = resultTypeExpression is not null && resultIsValueType;

        var stages = new List<(RegistrableTypeModel Type, StagedCallArm Arm, bool Direct, string? ExceptionFilter)>?[4];

        foreach (var candidate in types)
        {
            if (candidate.ContractShapes.IsEmpty)
            {
                continue;
            }

            for (var kindIndex = 0; kindIndex < 4; kindIndex++)
            {
                var kind = (DescriptorKind)(kindIndex + 1);

                // Deduped registrations of this candidate that reach the message —
                // mirrors the descriptor builders' first-wins (message, result) dedupe,
                // where result-agnostic async contracts carry `object`.
                string? matchedMessageKey = null;
                var matchedDirect = false;
                var registrationCount = 0;
                HashSet<string>? seenRegistrations = null;

                foreach (var shape in candidate.ContractShapes)
                {
                    if (shape.Kind != kind)
                    {
                        continue;
                    }

                    var direct = shape.MessageTypeExpression == message.TypeofExpression;

                    if (!direct && !message.AssignableKeys.Contains(shape.MessageTypeExpression))
                    {
                        continue;
                    }

                    var dedupeKey = shape.MessageTypeExpression + "\x1f"
                        + (kind == DescriptorKind.PreInterceptor
                            ? string.Empty
                            : shape.IsResultTyped ? shape.ResultTypeExpression : "object");

                    if ((seenRegistrations ??= new HashSet<string>(StringComparer.Ordinal)).Add(dedupeKey))
                    {
                        registrationCount++;
                        matchedMessageKey = shape.MessageTypeExpression;
                        matchedDirect = direct;
                    }
                }

                if (registrationCount == 0)
                {
                    continue;
                }

                // More than one registration would put the type into the stage more than
                // once; the order among them is not worth modeling — disqualify.
                if (registrationCount > 1)
                {
                    return false;
                }

                // Participation established. The participant itself must be modelable.
                if (!candidate.IsAccessible
                    || !candidate.DiscoveryKeys.IsEmpty
                    || candidate.GroupsExpression is not null
                    || candidate.IsNestedType)
                {
                    return false;
                }

                if (!TrySelectArm(candidate, kind, message, pipelineResultExpression, pipelineResultIsValueType,
                        out var arm, out var exceptionFilter))
                {
                    return false;
                }

                (stages[kindIndex] ??= []).Add((candidate, arm, matchedDirect, exceptionFilter));
                _ = matchedMessageKey;
            }
        }

        preCalls = OrderStage(stages[0], hasKeyedServiceExtensions);
        postCalls = OrderStage(stages[1], hasKeyedServiceExtensions);
        exceptionCalls = OrderStage(stages[2], hasKeyedServiceExtensions);
        finalCalls = OrderStage(stages[3], hasKeyedServiceExtensions);
        return true;
    }

    /// <summary>
    ///     Selects the pattern-match arm the runtime invoker would take for the
    ///     interceptor, or fails when none resolves (the runtime would throw
    ///     <c>NotSupportedException</c> — the strategy fallback preserves that) or when a
    ///     reference-typed pipeline result meets an inexactly-typed contract (possible
    ///     runtime result variance the string model cannot decide).
    /// </summary>
    private static bool TrySelectArm(
        RegistrableTypeModel candidate,
        DescriptorKind kind,
        RegistrableTypeModel message,
        string pipelineResultExpression,
        bool pipelineResultIsValueType,
        out StagedCallArm arm,
        out string? exceptionFilter)
    {
        arm = default;
        exceptionFilter = null;

        var hasAsyncTyped = false;
        var hasAsyncAgnostic = false;
        var hasSync = false;

        foreach (var shape in candidate.ContractShapes)
        {
            if (shape.Kind != kind)
            {
                continue;
            }

            // A filter the string model cannot reproduce takes the whole plan down: an
            // emitted call with no guard runs an interceptor that declined the exception,
            // and — worse — makes the stage count it as having handled one.
            if (shape.HasUndecidableExceptionFilter)
            {
                return false;
            }

            exceptionFilter = shape.ExceptionFilterExpression;

            // Message-side variance: exact match always works; a base/interface
            // registration matches only for reference-typed messages.
            var messageMatches = shape.MessageTypeExpression == message.TypeofExpression
                || (!message.IsValueType && message.AssignableKeys.Contains(shape.MessageTypeExpression));

            if (!messageMatches)
            {
                continue;
            }

            if (shape.IsResultTyped)
            {
                // Exact result match always works. An `object`-typed contract (the
                // flavored marker interfaces' shape) matches any reference-typed result
                // through the runtime's `in TResult` variance; value-typed results have
                // no variance, so the contract is simply invisible to the pattern match.
                if (shape.ResultTypeExpression == pipelineResultExpression
                    || (!pipelineResultIsValueType && shape.ResultTypeExpression == "object"))
                {
                    if (shape.IsAsync)
                    {
                        hasAsyncTyped = true;
                    }
                    else
                    {
                        hasSync = true;
                    }
                }
                else if (!pipelineResultIsValueType)
                {
                    // Any other base-of relationship the variance could admit is
                    // undecidable in the string model — disqualify.
                    return false;
                }
            }
            else if (shape.IsAsync)
            {
                hasAsyncAgnostic = true;
            }
            else
            {
                // Sync pre carries no result typing.
                hasSync = true;
            }
        }

        if (kind == DescriptorKind.PreInterceptor)
        {
            if (hasAsyncAgnostic)
            {
                arm = StagedCallArm.AsyncAgnostic;
                return true;
            }

            if (hasSync)
            {
                arm = StagedCallArm.Sync;
                return true;
            }

            return false;
        }

        if (hasAsyncTyped)
        {
            arm = StagedCallArm.AsyncTyped;
            return true;
        }

        if (hasAsyncAgnostic)
        {
            arm = StagedCallArm.AsyncAgnostic;
            return true;
        }

        if (hasSync)
        {
            arm = StagedCallArm.Sync;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Orders one staged stage exactly like the runtime shape builder: the direct
    ///     segment first, then the indirect one, each sorted by weight descending with the
    ///     type name as the ordinal tie-break (participants are non-nested and
    ///     non-generic, so the display name equals the runtime <c>Type.FullName</c>).
    /// </summary>
    private static ImmutableArray<StagedCallModel> OrderStage(
        List<(RegistrableTypeModel Type, StagedCallArm Arm, bool Direct, string? ExceptionFilter)>? entries,
        bool hasKeyedServiceExtensions)
    {
        if (entries is null)
        {
            return ImmutableArray<StagedCallModel>.Empty;
        }

        entries.Sort(static (x, y) =>
        {
            var bySegment = y.Direct.CompareTo(x.Direct);

            if (bySegment != 0)
            {
                return bySegment;
            }

            var byWeight = y.Type.Weight.CompareTo(x.Type.Weight);

            return byWeight != 0
                ? byWeight
                : string.CompareOrdinal(x.Type.DisplayName, y.Type.DisplayName);
        });

        var calls = ImmutableArray.CreateBuilder<StagedCallModel>(entries.Count);

        foreach (var (type, arm, _, exceptionFilter) in entries)
        {
            calls.Add(new StagedCallModel(
                type.TypeofExpression, arm, GatedConstructionExpression(type, hasKeyedServiceExtensions),
                exceptionFilter));
        }

        return calls.MoveToImmutable();
    }

    /// <summary>
    ///     Computes the compile-time void pipeline plans: a dispatchable command message
    ///     qualifies when the whole discovered pipeline for it is exactly one main-handler
    ///     descriptor, that descriptor is the result-less async contract
    ///     (<c>IAsyncHandler&lt;TMessage&gt;</c>), its handler participates in default
    ///     discovery in the default group, and no discovered interceptor targets the
    ///     message directly. The check is deliberately conservative and only ever costs
    ///     the speedup when wrong: the runtime executor validates the actual pipeline
    ///     against the container's selected frozen composition and falls back to the
    ///     general dispatch shape on any mismatch (covariant handlers or interceptors
    ///     selected through base contracts, or keyed selections).
    /// </summary>
    private static List<VoidPlanModel> ComputeVoidPlans(List<RegistrableTypeModel> types)
    {
        CollectPipelineFacts(types, out var handlerCounts, out var soleHandlers, out var interceptedMessages);

        var plans = new List<VoidPlanModel>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || !type.IsCommand)
            {
                continue;
            }

            if (!TryGetSolePlannableHandler(type, handlerCounts, soleHandlers, interceptedMessages,
                    out var handler, out var descriptor))
            {
                continue;
            }

            // The sole handler must be the async void contract.
            if (descriptor.ResultTypeExpression != ValueTaskExpression)
            {
                continue;
            }

            plans.Add(new VoidPlanModel(
                type.TypeofExpression,
                handler.TypeofExpression,
                handler.IsDirectlyConstructible,
                handler.ProviderConstructionExpression,
                handler.ProviderConstructionUsesKeyedServices));
        }

        return plans;
    }

    /// <summary>
    ///     Result-producing counterpart of <see cref="ComputeVoidPlans"/>: a dispatchable
    ///     command/query with exactly one closed, non-stream result contract qualifies
    ///     when its whole discovered pipeline is a single async handler producing exactly
    ///     that result. Equally conservative and equally advisory — the runtime validates
    ///     it against the container's selected frozen composition.
    /// </summary>
    private static List<ResultPlanModel> ComputeResultPlans(List<RegistrableTypeModel> types)
    {
        CollectPipelineFacts(types, out var handlerCounts, out var soleHandlers, out var interceptedMessages);

        var plans = new List<ResultPlanModel>();

        foreach (var type in types)
        {
            if (!type.IsDispatchableMessage || (!type.IsCommand && !type.IsQuery))
            {
                continue;
            }

            // Exactly one closed result contract, and not a streaming one — a message
            // with several result shapes is dispatched with executor-side result typing
            // the plan cannot pin down.
            if (type.DispatchResults.Length != 1 || type.DispatchResults[0].IsStream)
            {
                continue;
            }

            var dispatchResult = type.DispatchResults[0];

            if (!TryGetSolePlannableHandler(type, handlerCounts, soleHandlers, interceptedMessages,
                    out var handler, out var descriptor))
            {
                continue;
            }

            // The sole handler must be the async contract producing exactly the message's
            // declared result (sync contracts carry the bare result type and fall out).
            if (descriptor.ResultTypeExpression != ValueTaskExpression + "<" + dispatchResult.ResultTypeExpression + ">")
            {
                continue;
            }

            plans.Add(new ResultPlanModel(
                type.TypeofExpression,
                dispatchResult.ResultTypeExpression,
                handler.TypeofExpression,
                handler.IsDirectlyConstructible,
                handler.ProviderConstructionExpression,
                handler.ProviderConstructionUsesKeyedServices));
        }

        return plans;
    }

    /// <summary>
    ///     Indexes the discovered descriptors by message type: main-handler counts, the
    ///     (last-seen) sole handler per message, and the set of messages any interceptor
    ///     targets directly — the shared facts both plan computations qualify against.
    /// </summary>
    private static void CollectPipelineFacts(
        List<RegistrableTypeModel> types,
        out Dictionary<string, int> handlerCounts,
        out Dictionary<string, (RegistrableTypeModel Model, DescriptorModel Descriptor)> soleHandlers,
        out HashSet<string> interceptedMessages)
    {
        handlerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        soleHandlers = new Dictionary<string, (RegistrableTypeModel, DescriptorModel)>(StringComparer.Ordinal);
        interceptedMessages = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            foreach (var descriptor in type.Descriptors)
            {
                if (descriptor.Kind == DescriptorKind.MainHandler)
                {
                    handlerCounts.TryGetValue(descriptor.MessageTypeExpression, out var count);
                    handlerCounts[descriptor.MessageTypeExpression] = count + 1;
                    soleHandlers[descriptor.MessageTypeExpression] = (type, descriptor);
                }
                else
                {
                    interceptedMessages.Add(descriptor.MessageTypeExpression);
                }
            }
        }
    }

    /// <summary>
    ///     The shared plan qualification: the message has exactly one discovered main
    ///     handler, no interceptor targets it directly, and that handler is accessible
    ///     and discoverable by default (an unkeyed, ungrouped registration — anything
    ///     else may not be registered, or not in the default-group pipeline the plan
    ///     serves).
    /// </summary>
    private static bool TryGetSolePlannableHandler(
        RegistrableTypeModel type,
        Dictionary<string, int> handlerCounts,
        Dictionary<string, (RegistrableTypeModel Model, DescriptorModel Descriptor)> soleHandlers,
        HashSet<string> interceptedMessages,
        out RegistrableTypeModel handler,
        out DescriptorModel descriptor)
    {
        handler = default;
        descriptor = default;

        if (!handlerCounts.TryGetValue(type.TypeofExpression, out var count) || count != 1)
        {
            return false;
        }

        if (HasCovariantMainHandler(type, handlerCounts))
        {
            return false;
        }

        if (interceptedMessages.Contains(type.TypeofExpression))
        {
            return false;
        }

        (handler, descriptor) = soleHandlers[type.TypeofExpression];

        return handler.IsAccessible
               && handler.DiscoveryKeys.IsEmpty
               && handler.GroupsExpression is null;
    }

    /// <summary>
    ///     Whether any main handler is registered against a base type or interface of the
    ///     message. Single-handler mediation treats those as candidates alongside the
    ///     direct ones, so a message that has both is contested and must fail its dispatch
    ///     — something a plan, which bakes one handler in, cannot express. Disqualifying
    ///     here keeps the compiled lane and the reflective one telling the same story.
    /// </summary>
    /// <remarks>
    ///     Deliberately blunt: a message whose only handler is covariantly matched is
    ///     dispatchable and could in principle be planned (<c>IAsyncHandler</c>'s
    ///     <c>in TMessage</c> variance admits the base-typed handler), but the model has no
    ///     way to prove the supertype registration is the only one the runtime will see.
    ///     Those dispatches take the reflective path, as they did before covariance reached
    ///     main handlers at all.
    /// </remarks>
    private static bool HasCovariantMainHandler(RegistrableTypeModel type, Dictionary<string, int> handlerCounts)
    {
        foreach (var assignableKey in type.AssignableKeys)
        {
            if (handlerCounts.ContainsKey(assignableKey))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddModels(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> models,
        HashSet<string> seen,
        List<RegistrableTypeModel> types,
        List<RegistrableTypeModel> excludedShadows,
        DefaultResultAdapterSiteModel? defaultResultAdapter)
    {
        foreach (var model in models)
        {
            if (!seen.Add(model.TypeofExpression))
            {
                continue;
            }

            if (model.IsExcludedFromDiscovery)
            {
                // Deliberate opt-out: no registration, no diagnostics — the shadow only
                // feeds the reachability judgment's exclusion zone.
                excludedShadows.Add(model);
                continue;
            }

            if (!model.IsAccessible)
            {
                context.ReportDiagnostic(model.ReferencedAssemblyName is { } referencedAssembly
                    ? Diagnostic.Create(
                        GeneratorDiagnostics.InvisibleReferencedRegistrableType,
                        location: null,
                        model.DisplayName,
                        referencedAssembly)
                    : Diagnostic.Create(
                        GeneratorDiagnostics.InaccessibleRegistrableType,
                        model.Location?.ToLocation(),
                        model.DisplayName));
                continue;
            }

            if (model.HasMultiplePublicConstructors)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.MultiplePublicConstructors,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName));
            }

            if (model.HasFromServicesConstructorParameter)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.FromServicesOnConstructor,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName));
            }

            // ERGOSG011/012 judge where the message is compiled: a referenced message's
            // annotations were already judged (or predate the rules) in its own build.
            if (model.ReferencedAssemblyName is null && model.ResultAdapter is { } resultAdapter)
            {
                if (model.HasIgnoredResultAdapter)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.ConflictingResultAdapterAnnotations,
                        model.InfoLocation?.ToLocation(),
                        model.DisplayName));
                }
                else if (!resultAdapter.IsInstantiable || !resultAdapter.FitsDeclaredSlot)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.UnbindableResultAdapter,
                        model.InfoLocation?.ToLocation(),
                        resultAdapter.DisplayName,
                        model.DisplayName,
                        resultAdapter.IsInstantiable
                            ? "it does not implement IResultAdapter<TResult> for any result slot the message dispatches"
                            : "the runtime binding cannot instantiate it — a concrete, fully closed type with a " +
                              "public parameterless constructor is required"));
                }
            }

            // ERGOSG013/014: with a default adapter configured, a result-bearing message
            // no tier serves stays a throwing pipeline. Unacknowledged, that is a design
            // hole and fails the build right here — no reason to wait for a dispatch to
            // reveal it; acknowledged via [IgnoreResultAdapter], it stays visible as a
            // warning.
            if (model.ReferencedAssemblyName is null
                && defaultResultAdapter is not null
                && model.IsDispatchableMessage
                && model.ResultAdapter is null
                && HasUnservedResultSlots(model, defaultResultAdapter, out var unservedSlot))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    model.HasIgnoredResultAdapter
                        ? GeneratorDiagnostics.AcknowledgedThrowingPipeline
                        : GeneratorDiagnostics.UnservedByDefaultResultAdapter,
                    model.InfoLocation?.ToLocation(),
                    model.DisplayName,
                    unservedSlot,
                    defaultResultAdapter.BaseTypeExpression));
            }

            types.Add(model);
        }
    }

    /// <summary>
    ///     Pre-computes the handler descriptors for the type's handler contracts, mirroring
    ///     the runtime descriptor builders exactly: main handlers keep their declared
    ///     message types verbatim (sync contracts first, then result-less async, then
    ///     result-producing async, no dedupe), interceptors normalize generic messages to
    ///     their definitions and dedupe per (message, result) pair with the synchronous
    ///     pattern winning.
    /// </summary>
    /// <remarks>
    ///     A generic participant definition is modelled like any other: its message
    ///     expressions carry type parameters (<c>Wrap&lt;T&gt;</c>), which is fine because
    ///     nothing emits them — the composition table matches on the definition key and
    ///     names the participant by its own unbound <c>typeof</c>, closing it over the
    ///     dispatched message's arguments at runtime. Descriptors were empty here while
    ///     they were still emitted as <c>typeof</c> arguments, which a type parameter
    ///     cannot appear in; that emission is gone. A generic <em>containing</em> type is
    ///     still refused: its parameters are not the participant's own, so closing over
    ///     the message cannot supply them.
    /// </remarks>
    private static ImmutableArray<DescriptorModel> BuildDescriptors(INamedTypeSymbol symbol)
    {
        for (var current = symbol.ContainingType; current is not null; current = current.ContainingType)
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
    ///     The declared <c>[Group]</c> names, empty when the type declares none; the name
    ///     source behind <see cref="GetGroupsExpression"/>.
    /// </summary>
    private static ImmutableArray<string> GetGroupNames(INamedTypeSymbol symbol)
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
                return ImmutableArray<string>.Empty;
            }

            var names = ImmutableArray.CreateBuilder<string>(values.Length);

            foreach (var value in values)
            {
                if (value.Value is string name)
                {
                    names.Add(name);
                }
            }

            return names.ToImmutable();
        }

        return ImmutableArray<string>.Empty;
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
