using System;
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
    private const string DispatchRootsMetadataName = "Stella.Ergosfare.Core.Abstractions.GeneratedDispatchRoots";
    private const string DescriptorFactoryMetadataName = "Stella.Ergosfare.Core.Abstractions.Registry.Descriptors.HandlerDescriptors";
    private const string CommandBuilderMetadataName = "Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder";
    private const string QueryBuilderMetadataName = "Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder";
    private const string EventBuilderMetadataName = "Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder";

    private const string ValueTaskExpression = "global::System.Threading.Tasks.ValueTask";
    private const string DescriptorCatalogMetadataName = "Stella.Ergosfare.Core.Abstractions.GeneratedDescriptorCatalog";

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

        // Required members anywhere in the hierarchy make an emitted `new()` a compile
        // error (CS9035); the reflection-based container activation the plan replaces
        // does not enforce them, so such types stay on the container path.
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

        // Exactly one instance constructor, public and parameterless: with any richer
        // constructor present (records' synthesized copy constructor included), the
        // container's selection and `new()` can diverge — dropping dependencies the
        // container would have injected.
        return symbol.InstanceConstructors.Length == 1
               && symbol.InstanceConstructors[0] is { Parameters.IsEmpty: true, DeclaredAccessibility: Accessibility.Public };
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
            Descriptors = descriptors,
            ReferencedAssemblyName = null,
            DiscoveryKeys = GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            DispatchResults = isDispatchable ? GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = isAccessible && IsDirectlyConstructible(symbol),
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

            var model = new DispatchResultModel(VerbatimTypeExpression(iface.TypeArguments[0]), isStream);

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
            Descriptors = descriptors,
            ReferencedAssemblyName = assemblyName,
            DiscoveryKeys = GetDiscoveryKeys(symbol),
            IsDispatchableMessage = isDispatchable,
            DispatchResults = isDispatchable ? GetDispatchResults(symbol) : ImmutableArray<DispatchResultModel>.Empty,
            IsDirectlyConstructible = isAccessible && IsDirectlyConstructible(symbol),
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

        var source = RegistrationEmitter.Emit(types, availability, voidPlans, resultPlans, GeneratorVersion);
        context.AddSource("ErgosfareRegistrations.g.cs", SourceText.From(source, Encoding.UTF8));
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

            plans.Add(new VoidPlanModel(type.TypeofExpression, handler.TypeofExpression, handler.IsDirectlyConstructible));
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
                handler.IsDirectlyConstructible));
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
