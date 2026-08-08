using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
///     <para>
///     Phase 2: for types with handler contracts, the generator pre-computes the handler
///     descriptors (message type, result carrier, weight, groups) that the runtime
///     descriptor builders would otherwise derive reflectively, and registers them through
///     <c>IMessageRegistry.RegisterDescriptors</c> / the module builders'
///     <c>RegisterDescriptors</c>. Plain messages and open generic types (whose contract
///     type arguments cannot appear in <c>typeof</c>) fall back to <c>Register(Type)</c>;
///     both paths are mutually idempotent in the registry. Against older Ergosfare packages
///     that lack the descriptor surface, emission degrades to pure <c>Register(Type)</c>
///     calls.
///     </para>
///     <para>
///     Reference scanning: the generator also walks referenced assemblies for marker
///     types, replacing cross-assembly <c>RegisterFromAssembly</c> calls — a library's
///     handlers register through the consuming project's generated code. Only assemblies
///     that themselves reference Ergosfare are inspected (nothing else can implement a
///     marker), and Ergosfare's own assemblies are excluded because their handler contract
///     interfaces inherit the module markers. Types the generated code cannot name —
///     internal without <c>InternalsVisibleTo</c> covering this compilation — surface as
///     ERGOSG002 instead of diverging silently from the runtime scan. Opt out per project
///     with the <c>ErgosfareSourceGeneratorScanReferences=false</c> MSBuild property.
///     </para>
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
    private const string DispatchRootsMetadataName = "Stella.Ergosfare.Core.Abstractions.DispatchRoots.GeneratedDispatchRoots";
    private const string DescriptorFactoryMetadataName = "Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.HandlerDescriptors";
    private const string CommandBuilderMetadataName = "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";
    private const string QueryBuilderMetadataName = "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";
    private const string EventBuilderMetadataName = "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

    private const string ValueTaskExpression = "global::System.Threading.Tasks.ValueTask";
    private const string DescriptorCatalogMetadataName = "Stella.Ergosfare.Core.Abstractions.GeneratedDescriptorCatalog";

    private const string ServiceProviderExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";
    private const string KeyedServiceExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions";
    private const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";

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
                HasMessageRegistry: compilation.GetTypeByMetadataName(MessageRegistryMetadataName) is not null,
                HasCommandModuleBuilder: commandBuilder is not null,
                HasQueryModuleBuilder: queryBuilder is not null,
                HasEventModuleBuilder: eventBuilder is not null,
                HasDescriptorFactory: compilation.GetTypeByMetadataName(DescriptorFactoryMetadataName) is not null,
                CommandBuilderHasRegisterDescriptors: HasRegisterDescriptors(commandBuilder),
                QueryBuilderHasRegisterDescriptors: HasRegisterDescriptors(queryBuilder),
                EventBuilderHasRegisterDescriptors: HasRegisterDescriptors(eventBuilder),
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
                HasDescriptorCatalog: compilation.GetTypeByMetadataName(DescriptorCatalogMetadataName) is not null);
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

        context.RegisterSourceOutput(
            registrableTypes.Combine(availability).Combine(referencedTypes),
            static (spc, pair) => Execute(spc, pair.Left.Left, pair.Left.Right, pair.Right));
    }

    private static bool HasRegisterDescriptors(INamedTypeSymbol? builder)
        => builder is not null && !builder.GetMembers("RegisterDescriptors").IsEmpty;

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

        // Parameterless construction stays on the cheaper Func<THandler> shape.
        if (publicConstructor is null || publicConstructor.Parameters.IsEmpty)
        {
            return null;
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

            var typeExpression = parameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            arguments.Add(keyLiteral is null
                ? "global::" + ServiceProviderExtensionsMetadataName + ".GetRequiredService<" + typeExpression + ">(provider)"
                : "global::" + KeyedServiceExtensionsMetadataName + ".GetRequiredKeyedService<" + typeExpression + ">(provider, " + keyLiteral + ")");

            usesKeyedServices |= keyLiteral is not null;
        }

        return "static provider => new " + handlerTypeExpression + "(" + string.Join(", ", arguments) + ")";
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
            return null;
        }

        var isAccessible = IsAccessibleFromGeneratedCode(symbol);
        var descriptors = isAccessible ? BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && IsDispatchableMessage(symbol, descriptors);
        var typeofExpression = BuildTypeofExpression(symbol);

        var usesKeyedServices = false;
        var providerConstruction = isAccessible
            ? GetProviderConstructionExpression(symbol, typeofExpression, symbol.ContainingAssembly, out usesKeyedServices)
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
            Descriptors = descriptors,
            ReferencedAssemblyName = null,
            DiscoveryKeys = GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            DispatchResults = isDispatchable ? GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = isAccessible && IsDirectlyConstructible(symbol),
            ProviderConstructionExpression = providerConstruction,
            ProviderConstructionUsesKeyedServices = usesKeyedServices,
            HasPipelineExclusion = HasPipelineExclusionAttribute(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            AssignableKeys = isDispatchable ? GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = isAccessible ? BuildContractShapes(symbol) : ImmutableArray<ContractShapeModel>.Empty,
        };
    }

    /// <summary>
    ///     Whether the type can appear as a dispatched message instance: a concrete,
    ///     fully closed class or struct with no handler contracts. Only such types get
    ///     dispatch roots — abstract types and interfaces never carry a runtime message's
    ///     type, handlers are never dispatched, and open generics cannot be rooted.
    /// </summary>
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
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])),
                "IAsyncExceptionInterceptor" when iface.Arity == 2 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: true, IsResultTyped: true,
                    NormalizedTypeExpression(arguments[0]), VerbatimTypeExpression(arguments[1])),
                "IAsyncExceptionInterceptor" when iface.Arity == 1 => new ContractShapeModel(
                    DescriptorKind.ExceptionInterceptor, IsAsync: true, IsResultTyped: false,
                    NormalizedTypeExpression(arguments[0]), null),
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
    ///     Discovers registrable marker types in the compilation's referenced assemblies,
    ///     replacing cross-assembly <c>RegisterFromAssembly</c> calls. Only assemblies that
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

            // A library can opt out of discovery wholesale.
            if (HasExcludeFromDiscovery(assembly.GetAttributes()))
            {
                continue;
            }

            var givesAccess = assembly.GivesAccessTo(compilation.Assembly);

            CollectNamespaceTypes(assembly.GlobalNamespace, assembly.Name, givesAccess, ref results, ct);
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
        ref ImmutableArray<RegistrableTypeModel>.Builder? results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol nestedNamespace)
            {
                CollectNamespaceTypes(nestedNamespace, assemblyName, givesAccess, ref results, ct);
            }
            else if (member is INamedTypeSymbol type)
            {
                CollectTypeAndNested(type, assemblyName, givesAccess, ref results);
            }
        }
    }

    private static void CollectTypeAndNested(
        INamedTypeSymbol type,
        string assemblyName,
        bool givesAccess,
        ref ImmutableArray<RegistrableTypeModel>.Builder? results)
    {
        if (TryCreateReferencedModel(type, assemblyName, givesAccess) is { } model)
        {
            (results ??= ImmutableArray.CreateBuilder<RegistrableTypeModel>()).Add(model);
        }

        foreach (var nested in type.GetTypeMembers())
        {
            CollectTypeAndNested(nested, assemblyName, givesAccess, ref results);
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
        bool givesAccess)
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

        if (IsExcludedFromDiscovery(symbol))
        {
            return null;
        }

        var isAccessible = IsVisibleToCompilation(symbol, givesAccess) && HasSpellableName(symbol);
        var descriptors = isAccessible ? BuildDescriptors(symbol) : ImmutableArray<DescriptorModel>.Empty;
        var isDispatchable = isAccessible && IsDispatchableMessage(symbol, descriptors);
        var typeofExpression = BuildTypeofExpression(symbol);

        // Referenced handlers get no current-assembly grant: their construction factory
        // qualifies only over fully public parameter types (IVT grants are not modeled).
        var usesKeyedServices = false;
        var providerConstruction = isAccessible
            ? GetProviderConstructionExpression(symbol, typeofExpression, currentAssembly: null, out usesKeyedServices)
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
            Descriptors = descriptors,
            ReferencedAssemblyName = assemblyName,
            DiscoveryKeys = GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            DispatchResults = isDispatchable ? GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = isAccessible && IsDirectlyConstructible(symbol),
            ProviderConstructionExpression = providerConstruction,
            ProviderConstructionUsesKeyedServices = usesKeyedServices,
            HasPipelineExclusion = HasPipelineExclusionAttribute(symbol),
            IsValueType = symbol.IsValueType,
            IsNestedType = symbol.ContainingType is not null,
            AssignableKeys = isDispatchable ? GetAssignableKeys(symbol) : ImmutableArray<string>.Empty,
            ContractShapes = isAccessible ? BuildContractShapes(symbol) : ImmutableArray<ContractShapeModel>.Empty,
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
        ImmutableArray<RegistrableTypeModel> referencedModels)
    {
        var seen = new HashSet<string>();
        var types = new List<RegistrableTypeModel>();

        // Source-declared types first: on a (pathological) full-name collision with a
        // referenced type, typeof in the generated file binds to the source declaration.
        AddModels(context, sourceModels, seen, types);
        AddModels(context, referencedModels, seen, types);

        if (types.Count == 0)
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
            ? ComputeStagedPlans(types)
            : (IReadOnlyList<StagedPlanModel>)Array.Empty<StagedPlanModel>();

        var source = RegistrationEmitter.Emit(types, availability, voidPlans, resultPlans, stagedPlans, GeneratorVersion);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
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
    ///     advisory regardless: the hosting executor re-validates the composition per
    ///     registry version.
    /// </summary>
    private static List<StagedPlanModel> ComputeStagedPlans(List<RegistrableTypeModel> types)
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

            if (!TryAssembleStagedStages(type, types, resultTypeExpression, resultIsValueType,
                    out var pre, out var post, out var exceptionCalls, out var finalCalls))
            {
                continue;
            }

            if (pre.Length + post.Length + exceptionCalls.Length + finalCalls.Length == 0)
            {
                // No interceptors: the single-handler plans already cover this shape.
                continue;
            }

            plans.Add(new StagedPlanModel(
                type.TypeofExpression,
                resultTypeExpression,
                resultIsValueType,
                handler.TypeofExpression,
                pre, post, exceptionCalls, finalCalls));
        }

        return plans;
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
    private static bool TryAssembleStagedStages(
        RegistrableTypeModel message,
        List<RegistrableTypeModel> types,
        string? resultTypeExpression,
        bool resultIsValueType,
        out ImmutableArray<StagedCallModel> preCalls,
        out ImmutableArray<StagedCallModel> postCalls,
        out ImmutableArray<StagedCallModel> exceptionCalls,
        out ImmutableArray<StagedCallModel> finalCalls)
    {
        preCalls = postCalls = exceptionCalls = finalCalls = ImmutableArray<StagedCallModel>.Empty;

        // The pipeline result the arms match against: the declared result for result
        // pipelines, the ValueTask carrier for void ones (a struct either way unless the
        // declared result is a reference type).
        var pipelineResultExpression = resultTypeExpression ?? ValueTaskExpression;
        var pipelineResultIsValueType = resultTypeExpression is null || resultIsValueType;

        var stages = new List<(RegistrableTypeModel Type, StagedCallArm Arm, bool Direct)>?[4];

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
                        out var arm))
                {
                    return false;
                }

                (stages[kindIndex] ??= []).Add((candidate, arm, matchedDirect));
                _ = matchedMessageKey;
            }
        }

        preCalls = OrderStage(stages[0]);
        postCalls = OrderStage(stages[1]);
        exceptionCalls = OrderStage(stages[2]);
        finalCalls = OrderStage(stages[3]);
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
        out StagedCallArm arm)
    {
        arm = default;

        var hasAsyncTyped = false;
        var hasAsyncAgnostic = false;
        var hasSync = false;

        foreach (var shape in candidate.ContractShapes)
        {
            if (shape.Kind != kind)
            {
                continue;
            }

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
        List<(RegistrableTypeModel Type, StagedCallArm Arm, bool Direct)>? entries)
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

        foreach (var (type, arm, _) in entries)
        {
            calls.Add(new StagedCallModel(type.TypeofExpression, arm));
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
    ///     the speedup when wrong: the runtime executor re-validates the actual pipeline
    ///     per registry version and falls back to the runtime dispatch shape on any
    ///     mismatch (covariant handlers or interceptors registered for base contracts,
    ///     keyed selections, runtime registrations).
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
    ///     that result. Equally conservative and equally advisory — the runtime
    ///     re-validates per registry version.
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

        if (interceptedMessages.Contains(type.TypeofExpression))
        {
            return false;
        }

        (handler, descriptor) = soleHandlers[type.TypeofExpression];

        return handler.IsAccessible
               && handler.DiscoveryKeys.IsEmpty
               && handler.GroupsExpression is null;
    }

    private static void AddModels(
        SourceProductionContext context,
        ImmutableArray<RegistrableTypeModel> models,
        HashSet<string> seen,
        List<RegistrableTypeModel> types)
    {
        foreach (var model in models)
        {
            if (!seen.Add(model.TypeofExpression))
            {
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

            types.Add(model);
        }
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
