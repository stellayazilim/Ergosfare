using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The plugin facade half of the generator.
/// </summary>
/// <remarks>
/// <para>
/// An assembly declaring itself a plugin with <c>[assembly: ErgosfarePlugin("Name")]</c> gets
/// the surface its consumers call emitted into its own compilation: an <c>IModule</c>
/// implementation and an <c>Add&lt;Name&gt;</c> extension on the module registry. An assembly
/// without the attribute gets no extra source at all.
/// </para>
/// <para>
/// This runs in the plugin's compilation, not the consumer's, and the two halves never
/// observe each other: the facade is compiled into the plugin assembly and called like any
/// other extension method, while the consumer's own generator reads the plugin's
/// <c>[PipelineInvokable]</c> methods from metadata. Nothing here needs one generator to see
/// another's output, which Roslyn does not offer.
/// </para>
/// </remarks>
public sealed partial class ErgosfareRegistrationGenerator
{
    private const string PluginsAbstractionsNamespace = "Stella.Ergosfare.Plugins.Abstractions";
    private const string ErgosfarePluginAttributeName = "ErgosfarePluginAttribute";
    private const string PipelineInvokableAttributeName = "PipelineInvokableAttribute";

    private const string ModuleMetadataName =
        "Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModule";

    private const string ModuleRegistryExpression =
        "global::Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModuleRegistry";

    private const string ModuleInterfaceExpression =
        "global::Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModule";

    private const string ModuleConfigurationExpression =
        "global::Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModuleConfiguration";

    private const string ServiceCollectionDescriptorExtensionsExpression =
        "global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions";

    private const string GetRequiredServiceExpression =
        "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService";

    /// <summary>
    /// The same id as <c>ExperimentalIds.PluginSurface</c>, written out here because the
    /// generator resolves nothing through a reference to either abstractions package.
    /// </summary>
    private const string PluginSurfaceExperimentalId = "ERGOEXP002";

    /// <summary>
    /// Wires the plugin facade output.
    /// </summary>
    /// <param name="context">The initialization context Roslyn supplies.</param>
    /// <remarks>
    /// Its own output rather than part of the registration emission: the two share no input,
    /// and a plugin assembly usually declares no dispatch of its own.
    /// </remarks>
    private static void RegisterPluginFacade(IncrementalGeneratorInitializationContext context)
    {
        var facade = context.CompilationProvider.Select(static (compilation, ct) => ReadPluginFacade(compilation, ct));

        context.RegisterSourceOutput(facade, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            foreach (var service in model.Value.Services)
            {
                if (service.CannotReceiveOptions)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        GeneratorDiagnostics.PluginServiceCannotReceiveOptions,
                        service.Location?.ToLocation() ?? Location.None,
                        service.DisplayName,
                        model.Value.OptionsDisplayName));
                }
            }

            spc.AddSource("ErgosfarePluginFacade.g.cs", EmitPluginFacade(model.Value));

            // A file of its own: the facade uses a file-scoped namespace, and these parts
            // belong to the services' namespaces rather than the facade's.
            if (EmitPluginServiceParts(model.Value) is { } parts)
            {
                spc.AddSource("ErgosfarePluginServices.g.cs", parts);
            }
        });
    }

    private const string ErgosfareContextMetadataName = "Stella.Ergosfare.Core.Abstractions.ErgosfareContext";
    private const string PluginServiceFilterAttributeName = "PluginServiceFilterAttribute";
    private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";

    /// <summary>
    /// Collects every <c>[PipelineInvokable]</c> method this compilation can see, declared
    /// here or carried by a scanned reference.
    /// </summary>
    /// <param name="compilation">The compilation to read.</param>
    /// <param name="scanReferences">Whether referenced assemblies are read too.</param>
    /// <param name="ct">Cancels the scan.</param>
    /// <returns>One model per hook method, reduced to what emission needs.</returns>
    /// <remarks>
    /// References are read under the same rules as the participant scan: an assembly not
    /// referencing Ergosfare cannot carry plugin methods, and one matching the reserved prefix
    /// is skipped unless it opts back in — which ERGO015 reports, so the skip is never silent.
    /// </remarks>
    private static ImmutableArray<PluginInvocationModel> ScanPluginInvocations(
        Compilation compilation,
        bool scanReferences,
        CancellationToken ct)
    {
        var context = compilation.GetTypeByMetadataName(ErgosfareContextMetadataName);

        if (context is null)
        {
            return ImmutableArray<PluginInvocationModel>.Empty;
        }

        var valueTask = compilation.GetTypeByMetadataName(ValueTaskMetadataName);

        ImmutableArray<PluginInvocationModel>.Builder? models = null;

        CollectPluginInvocations(compilation.Assembly.GlobalNamespace, context, valueTask, ref models, ct);

        if (scanReferences)
        {
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                ct.ThrowIfCancellationRequested();

                if (!ReferenceScanner.ReferencesErgosfare(assembly)
                    || (ReferenceScanner.IsErgosfareAssemblyName(assembly.Name) && !ReferenceScanner.HasForceScanReferencesOptIn(assembly)))
                {
                    continue;
                }

                CollectPluginInvocations(assembly.GlobalNamespace, context, valueTask, ref models, ct);
            }
        }

        return models?.ToImmutable() ?? ImmutableArray<PluginInvocationModel>.Empty;
    }

    /// <summary>
    /// Walks a namespace and everything nested under it for hook methods.
    /// </summary>
    /// <param name="namespace">The namespace to walk.</param>
    /// <param name="context">The <c>ErgosfareContext</c> type, for binding parameters.</param>
    /// <param name="valueTask">The <c>ValueTask</c> type, or <c>null</c> when unavailable.</param>
    /// <param name="models">The builder found methods are added to; created on first use.</param>
    /// <param name="ct">Cancels the walk.</param>
    private static void CollectPluginInvocations(
        INamespaceSymbol @namespace,
        INamedTypeSymbol context,
        INamedTypeSymbol? valueTask,
        ref ImmutableArray<PluginInvocationModel>.Builder? models,
        CancellationToken ct)
    {
        foreach (var member in @namespace.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            switch (member)
            {
                case INamespaceSymbol nested:
                    CollectPluginInvocations(nested, context, valueTask, ref models, ct);
                    break;

                case INamedTypeSymbol type:
                    CollectPluginInvocations(type, context, valueTask, ref models, ct);
                    break;
            }
        }
    }

    /// <summary>
    /// Reads one type's hook methods, and those of every type nested in it.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <param name="context">The <c>ErgosfareContext</c> type, for binding parameters.</param>
    /// <param name="valueTask">The <c>ValueTask</c> type, or <c>null</c> when unavailable.</param>
    /// <param name="models">The builder found methods are added to; created on first use.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <remarks>
    /// A type the emitted call cannot name contributes nothing, and neither does a hook
    /// declaration this emission cannot write a call for.
    /// </remarks>
    private static void CollectPluginInvocations(
        INamedTypeSymbol type,
        INamedTypeSymbol context,
        INamedTypeSymbol? valueTask,
        ref ImmutableArray<PluginInvocationModel>.Builder? models,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var nested in type.GetTypeMembers())
        {
            CollectPluginInvocations(nested, context, valueTask, ref models, ct);
        }

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false }
            || type.IsGenericType
            || !ConstructionAnalyzer.IsNameableClosedType(type, type.ContainingAssembly))
        {
            return;
        }

        var typeFilter = ReadServiceFilter(type.GetAttributes());
        var typeExpression = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var displayName = type.ToDisplayString();

        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                || method.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var methodFilter = ReadServiceFilter(method.GetAttributes());
            var filter = Intersect(typeFilter, methodFilter);

            // One type parameter, the message. No hook carries a result, so every hook has
            // the same shape, and any other arity is a declaration no call can be written for.
            if (method.Arity != 1)
            {
                continue;
            }

            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass is not { Name: PipelineInvokableAttributeName } attributeClass
                    || !SymbolNaming.IsInNamespace(attributeClass, PluginsAbstractionsNamespace)
                    || attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not int hook
                    || !Enum.IsDefined(typeof(PluginHook), hook))
                {
                    continue;
                }

                models ??= ImmutableArray.CreateBuilder<PluginInvocationModel>();
                models.Add(new PluginInvocationModel(
                    typeExpression,
                    displayName,
                    method.Name,
                    (PluginHook)hook,
                    IsAsync: valueTask is not null
                             && SymbolEqualityComparer.Default.Equals(method.ReturnType.OriginalDefinition, valueTask),
                    method.IsStatic,
                    filter.Modules,
                    filter.Keys,
                    BindParameters(method, context),
                    ReadConstraints(method),
                    Location: null));
            }
        }
    }

    /// <summary>
    /// Reads a hook method's generic constraints.
    /// </summary>
    /// <param name="method">The hook method to read.</param>
    /// <returns>
    /// The constraints, or a model marked unmodelable when they cannot all be reproduced.
    /// </returns>
    /// <remarks>
    /// This is how a plugin says which messages it applies to. The call is closed over each
    /// plan's concrete types when it is written, so a constraint is both a selection rule and
    /// something the emitted source has to satisfy: a plan whose message fails it would
    /// produce code the consumer's build rejects. Anything that cannot be decided from type
    /// names marks the whole method unmodelable rather than being passed over — a constructed
    /// generic constraint, for one, since a message's assignable chain is normalized to
    /// unbound definitions and <c>ICommand&lt;string&gt;</c> cannot be told from
    /// <c>ICommand&lt;int&gt;</c> there.
    /// </remarks>
    private static PluginConstraintModel ReadConstraints(IMethodSymbol method)
    {
        if (method.TypeParameters.IsEmpty)
        {
            return PluginConstraintModel.None;
        }

        var parameter = method.TypeParameters[0];

        if (parameter.HasConstructorConstraint || parameter.HasUnmanagedTypeConstraint)
        {
            return new PluginConstraintModel(ImmutableArray<string>.Empty, false, false, IsUnmodelable: true);
        }

        var types = ImmutableArray.CreateBuilder<string>(parameter.ConstraintTypes.Length);

        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (constraint is not INamedTypeSymbol { IsGenericType: false } named)
            {
                return new PluginConstraintModel(ImmutableArray<string>.Empty, false, false, IsUnmodelable: true);
            }

            types.Add(SymbolNaming.NormalizedTypeExpression(named));
        }

        return new PluginConstraintModel(
            types.ToImmutable(),
            parameter.HasReferenceTypeConstraint,
            parameter.HasValueTypeConstraint,
            IsUnmodelable: false);
    }

    /// <summary>
    /// Decides what each of a hook method's parameters is given when the call is written.
    /// </summary>
    /// <param name="method">The hook method to read.</param>
    /// <param name="context">The <c>ErgosfareContext</c> type.</param>
    /// <returns>One binding per parameter, in order.</returns>
    /// <remarks>
    /// The message comes from the method's own type parameter, the context from the plan's
    /// locals, and anything else from the dispatching provider.
    /// </remarks>
    private static ImmutableArray<PluginParameterBinding> BindParameters(
        IMethodSymbol method,
        INamedTypeSymbol context)
    {
        if (method.Parameters.IsEmpty)
        {
            return ImmutableArray<PluginParameterBinding>.Empty;
        }

        var bindings = ImmutableArray.CreateBuilder<PluginParameterBinding>(method.Parameters.Length);

        foreach (var parameter in method.Parameters)
        {
            var kind = parameter.Type switch
            {
                ITypeParameterSymbol { Ordinal: 0 } p when p.DeclaringMethod is not null => PluginParameterKind.Message,
                INamedTypeSymbol named when SymbolEqualityComparer.Default.Equals(named, context)
                    => PluginParameterKind.Context,
                _ => PluginParameterKind.Service,
            };

            bindings.Add(new PluginParameterBinding(
                kind,
                kind == PluginParameterKind.Service
                    ? parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    : null));
        }

        return bindings.MoveToImmutable();
    }

    /// <summary>
    /// Reads the module families and discovery keys a <c>[PluginServiceFilter]</c> names.
    /// </summary>
    /// <param name="attributes">The attributes to search.</param>
    /// <returns>
    /// The declared filter, or every family and no key when the attribute is absent.
    /// </returns>
    private static (PluginModule Modules, ImmutableArray<string> Keys) ReadServiceFilter(
        ImmutableArray<AttributeData> attributes)
    {
        var modules = PluginModule.All;
        var keys = ImmutableArray<string>.Empty;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { Name: PluginServiceFilterAttributeName } attributeClass
                || !SymbolNaming.IsInNamespace(attributeClass, PluginsAbstractionsNamespace)
                || attribute.ConstructorArguments.Length != 1)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];

            if (argument.Kind == TypedConstantKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<string>();

                foreach (var value in argument.Values)
                {
                    if (value.Value is string key)
                    {
                        builder.Add(key);
                    }
                }

                keys = keys.AddRange(builder);
            }
            else if (argument.Value is int flags)
            {
                modules &= (PluginModule)flags;
            }
        }

        return (modules, keys);
    }

    /// <summary>
    /// Combines a type's filter with one of its methods'.
    /// </summary>
    /// <param name="type">The filter declared on the type.</param>
    /// <param name="method">The filter declared on the method.</param>
    /// <returns>The combined filter.</returns>
    /// <remarks>
    /// A method-level filter narrows the type's rather than replacing it: families intersect,
    /// while keys are added together, each named key being another set of plans the plugin
    /// opts into.
    /// </remarks>
    private static (PluginModule Modules, ImmutableArray<string> Keys) Intersect(
        (PluginModule Modules, ImmutableArray<string> Keys) type,
        (PluginModule Modules, ImmutableArray<string> Keys) method)
        => (type.Modules & method.Modules, type.Keys.IsEmpty ? method.Keys : type.Keys.AddRange(method.Keys));

    /// <summary>
    /// Wires the ERGO015 output: the plugin packages the reserved-prefix rule leaves out of
    /// reference scanning.
    /// </summary>
    /// <param name="context">The initialization context Roslyn supplies.</param>
    /// <remarks>
    /// The exclusion is right for Ergosfare's own assemblies, whose marker-inheriting contract
    /// interfaces have to stay out of generated registrations, but a plugin named under the
    /// same prefix becomes a silent no-op. What is tested is the assembly's own
    /// <c>[ErgosfarePlugin]</c> declaration rather than its types: an assembly that never
    /// called itself a plugin is not one being dropped, and the test stays a single lookup per
    /// reference.
    /// </remarks>
    private static void RegisterPluginScanDiagnostics(IncrementalGeneratorInitializationContext context)
    {
        var scanReferences = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            !provider.GlobalOptions.TryGetValue(ScanReferencesBuildProperty, out var value)
            || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase));

        var excluded = context.CompilationProvider
            .Combine(scanReferences)
            .Select(static (pair, ct) => pair.Right
                ? FindPrefixExcludedPlugins(pair.Left, ct)
                : ImmutableArray<string>.Empty);

        context.RegisterSourceOutput(excluded, static (spc, names) =>
        {
            foreach (var name in names)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.PluginUnderReservedPrefix, Location.None, name));
            }
        });
    }

    /// <summary>
    /// Finds the referenced plugin assemblies the reserved prefix keeps out of scanning.
    /// </summary>
    /// <param name="compilation">The compilation whose references are checked.</param>
    /// <param name="ct">Cancels the search.</param>
    /// <returns>The names of the excluded plugin assemblies.</returns>
    private static ImmutableArray<string> FindPrefixExcludedPlugins(Compilation compilation, CancellationToken ct)
    {
        ImmutableArray<string>.Builder? excluded = null;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            if (!ReferenceScanner.IsErgosfareAssemblyName(assembly.Name)
                || ReferenceScanner.HasForceScanReferencesOptIn(assembly)
                || ReadPluginDeclaration(assembly) is null)
            {
                continue;
            }

            excluded ??= ImmutableArray.CreateBuilder<string>();
            excluded.Add(assembly.Name);
        }

        return excluded?.ToImmutable() ?? ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Reads the plugin declaration and the services behind it.
    /// </summary>
    /// <param name="compilation">The compilation to read.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>
    /// The facade to emit, or <c>null</c> when this assembly is not a plugin or does not
    /// reference the module surface the facade implements.
    /// </returns>
    /// <remarks>
    /// A service is any accessible type in this compilation carrying at least one
    /// <c>[PipelineInvokable]</c> method. They come back ordered by type name.
    /// </remarks>
    private static PluginFacadeModel? ReadPluginFacade(Compilation compilation, CancellationToken ct)
    {
        var declaration = ReadPluginDeclaration(compilation.Assembly);

        if (declaration is null || compilation.GetTypeByMetadataName(ModuleMetadataName) is null)
        {
            return null;
        }

        var services = ImmutableArray.CreateBuilder<PluginServiceModel>();

        CollectPluginServices(compilation.Assembly.GlobalNamespace, declaration.Value.Options, services, ct);

        var ordered = services.ToImmutable()
            .Sort(static (x, y) => string.CompareOrdinal(x.TypeExpression, y.TypeExpression));

        return new PluginFacadeModel(
            declaration.Value.Name,
            declaration.Value.Options?.TypeExpression,
            declaration.Value.Options?.DisplayName ?? string.Empty,
            ordered);
    }

    /// <summary>
    /// Reads an assembly's <c>[assembly: ErgosfarePlugin]</c> declaration.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>
    /// The plugin's name and its options type, or <c>null</c> when the assembly declares no
    /// plugin.
    /// </returns>
    /// <remarks>
    /// The options argument is optional; a plugin taking no settings simply names none.
    /// </remarks>
    private static (string Name, PluginOptionsType? Options)? ReadPluginDeclaration(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { Name: ErgosfarePluginAttributeName } attributeClass
                || !SymbolNaming.IsInNamespace(attributeClass, PluginsAbstractionsNamespace)
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not string name
                || name.Length == 0)
            {
                continue;
            }

            PluginOptionsType? options = null;

            if (attribute.ConstructorArguments.Length > 1
                && attribute.ConstructorArguments[1].Value is INamedTypeSymbol optionsType)
            {
                options = new PluginOptionsType(
                    optionsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    optionsType.ToDisplayString(),
                    optionsType);
            }

            return (name, options);
        }

        return null;
    }

    /// <summary>
    /// Walks a namespace for types declaring <c>[PipelineInvokable]</c> methods.
    /// </summary>
    /// <param name="namespace">The namespace to walk.</param>
    /// <param name="options">The plugin's options type, when it declares one.</param>
    /// <param name="services">The builder found services are added to.</param>
    /// <param name="ct">Cancels the walk.</param>
    /// <remarks>
    /// A type the generated facade cannot name — private nested, file-local — is passed over
    /// silently: the consumer-side scan reports it, so the facade needs no diagnostic of its
    /// own.
    /// </remarks>
    private static void CollectPluginServices(
        INamespaceSymbol @namespace,
        PluginOptionsType? options,
        ImmutableArray<PluginServiceModel>.Builder services,
        CancellationToken ct)
    {
        foreach (var member in @namespace.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            switch (member)
            {
                case INamespaceSymbol nested:
                    CollectPluginServices(nested, options, services, ct);
                    break;

                case INamedTypeSymbol type:
                    CollectPluginServices(type, options, services, ct);
                    break;
            }
        }
    }

    /// <summary>
    /// Reads one type, and every type nested in it, as a possible plugin service.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <param name="options">The plugin's options type, when it declares one.</param>
    /// <param name="services">The builder found services are added to.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <remarks>
    /// One hook method is enough; the type is added once however many it declares.
    /// </remarks>
    private static void CollectPluginServices(
        INamedTypeSymbol type,
        PluginOptionsType? options,
        ImmutableArray<PluginServiceModel>.Builder services,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var nested in type.GetTypeMembers())
        {
            CollectPluginServices(nested, options, services, ct);
        }

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false }
            || type.IsGenericType
            || !ConstructionAnalyzer.IsNameableClosedType(type, type.ContainingAssembly))
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method && HasPipelineInvokable(method))
            {
                services.Add(ReadPluginService(type, options, ct));
                return;
            }
        }
    }

    /// <summary>
    /// Decides how the module will construct one plugin service.
    /// </summary>
    /// <param name="type">The service type.</param>
    /// <param name="options">The plugin's options type, when it declares one.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>
    /// The service model, which reports whether the options can reach it at all.
    /// </returns>
    /// <remarks>
    /// With no options type the module registers the type and lets the container activate it.
    /// With one, the options instance never enters the container — the module holds it and
    /// hands it to a baked <c>new</c> — so what is decided here is which constructor that
    /// <c>new</c> calls and what each of its parameters is given.
    /// </remarks>
    private static PluginServiceModel ReadPluginService(
        INamedTypeSymbol type,
        PluginOptionsType? options,
        CancellationToken ct)
    {
        var typeExpression = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var displayName = type.ToDisplayString();

        if (options is not { } declared)
        {
            return new PluginServiceModel(typeExpression, displayName);
        }

        var constructors = type.InstanceConstructors
            .Where(static c => !c.IsImplicitlyDeclared && c.DeclaredAccessibility == Accessibility.Public)
            .ToArray();

        // The author wrote a constructor, so it is the contract, and its parameters are given
        // what a hook method's are: the options from the module's own instance, everything
        // else from the container.
        if (constructors.Length == 1)
        {
            var arguments = ImmutableArray.CreateBuilder<string?>(constructors[0].Parameters.Length);
            var bindsOptions = false;

            foreach (var parameter in constructors[0].Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, declared.Symbol))
                {
                    arguments.Add(null);
                    bindsOptions = true;
                }
                else
                {
                    arguments.Add(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                }
            }

            return new PluginServiceModel(
                typeExpression,
                displayName,
                ConstructorArguments: arguments.MoveToImmutable(),
                CannotReceiveOptions: !bindsOptions,
                Location: bindsOptions ? null : LocationInfo.From(type));
        }

        // Several constructors leave nothing to choose between; the emission will not guess.
        if (constructors.Length > 1)
        {
            return new PluginServiceModel(
                typeExpression, displayName,
                CannotReceiveOptions: true, Location: LocationInfo.From(type));
        }

        // No constructor at all. A partial class gets the field and the one-line constructor
        // written for it; anything else has no way in, and says so.
        if (!IsPartial(type, ct) || type.ContainingType is not null)
        {
            return new PluginServiceModel(
                typeExpression, displayName,
                CannotReceiveOptions: true, Location: LocationInfo.From(type));
        }

        return new PluginServiceModel(
            typeExpression,
            displayName,
            ConstructorArguments: ImmutableArray.Create<string?>([null]),
            GeneratedNamespace: type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(),
            GeneratedTypeName: type.Name,
            GeneratedTypeKeyword: type.IsRecord ? "record" : "class");
    }

    /// <summary>
    /// Reports whether a type is declared <c>partial</c>.
    /// </summary>
    /// <param name="type">The type to test.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns><c>true</c> when a source declaration carries the modifier.</returns>
    private static bool IsPartial(INamedTypeSymbol type, CancellationToken ct)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(ct) is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax declaration
                && declaration.Modifiers.Any(static m =>
                    m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether a method is a plugin hook.
    /// </summary>
    /// <param name="method">The method to test.</param>
    /// <returns><c>true</c> when it carries <c>[PipelineInvokable]</c>.</returns>
    /// <remarks>
    /// Only whether the declaring type needs registering; which hook it is and what shape it
    /// has are read elsewhere.
    /// </remarks>
    private static bool HasPipelineInvokable(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: PipelineInvokableAttributeName } attributeClass
                && SymbolNaming.IsInNamespace(attributeClass, PluginsAbstractionsNamespace))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the plugin's module and the registry extension that installs it.
    /// </summary>
    /// <param name="model">The facade to write.</param>
    /// <returns>The generated source.</returns>
    /// <remarks>
    /// Services are registered as singletons: a plugin service is resolved once per container
    /// and lives as long as the composition it is baked into, so no dispatch ever resolves
    /// one. Its per-dispatch dependencies arrive as method parameters instead, which is why
    /// the lifetime is fixed here rather than left to the author.
    /// </remarks>
    private static string EmitPluginFacade(PluginFacadeModel model)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.Append("namespace ").Append(PluginsAbstractionsNamespace).AppendLine(".Generated;");
        sb.AppendLine();

        sb.AppendLine("/// <summary>The plugin's module: registers the services carrying its pipeline methods.</summary>");
        sb.Append("[global::System.CodeDom.Compiler.GeneratedCode(\"Stella.Ergosfare.SourceGenerator\", \"")
            .Append(GeneratorVersion.Value).AppendLine("\")]");
        sb.Append("internal sealed class ").Append(model.Name).Append("Module : ")
            .Append(ModuleInterfaceExpression).AppendLine();
        sb.AppendLine("{");

        if (model.OptionsTypeExpression is { } optionsType)
        {
            // The module holds the instance the consumer passed. Nothing registers it, so the
            // container carries no entry for it and no resolution looks it up.
            sb.Append("    private readonly ").Append(optionsType).AppendLine(" _options;");
            sb.AppendLine();
            sb.Append("    public ").Append(model.Name).Append("Module(").Append(optionsType)
                .AppendLine(" options)");
            sb.AppendLine("    {");
            sb.AppendLine("        _options = options;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.Append("    public void Build(").Append(ModuleConfigurationExpression).AppendLine(" configuration)");
        sb.AppendLine("    {");

        if (model.Services.IsEmpty)
        {
            sb.AppendLine("        // The plugin declares no [PipelineInvokable] methods yet — nothing to register.");
        }
        else
        {
            foreach (var service in model.Services)
            {
                sb.Append("        ").Append(ServiceCollectionDescriptorExtensionsExpression);

                if (service.ConstructorArguments.IsDefault)
                {
                    // Nothing to hand over, so the container activates the type itself.
                    sb.Append(".TryAddSingleton<").Append(service.TypeExpression).AppendLine(">(configuration.Services);");
                    continue;
                }

                sb.Append(".TryAddSingleton<").Append(service.TypeExpression)
                    .Append(">(configuration.Services, serviceProvider => new ")
                    .Append(service.TypeExpression).Append('(');

                for (var i = 0; i < service.ConstructorArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    if (service.ConstructorArguments[i] is { } dependency)
                    {
                        sb.Append(GetRequiredServiceExpression).Append('<').Append(dependency)
                            .Append(">(serviceProvider)");
                    }
                    else
                    {
                        sb.Append("_options");
                    }
                }

                sb.AppendLine("));");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>Adds this plugin to an Ergosfare module registry.</summary>");
        sb.Append("[global::System.CodeDom.Compiler.GeneratedCode(\"Stella.Ergosfare.SourceGenerator\", \"")
            .Append(GeneratorVersion.Value).AppendLine("\")]");
        // Where the consumer opts in: installing a plugin means using the experimental
        // surface, and this extension is the one line of it they write themselves.
        sb.Append("[global::System.Diagnostics.CodeAnalysis.Experimental(\"")
            .Append(PluginSurfaceExperimentalId).AppendLine("\")]");
        sb.Append("public static class ").Append(model.Name).AppendLine("PluginModuleRegistryExtensions");
        sb.AppendLine("{");
        sb.Append("    public static ").Append(ModuleRegistryExpression)
            .Append(" Add").Append(model.Name).Append("(this ").Append(ModuleRegistryExpression)
            .Append(" moduleRegistry");

        if (model.OptionsTypeExpression is { } parameterType)
        {
            sb.Append(", ").Append(parameterType).Append(" options");
        }

        sb.AppendLine(")");
        sb.AppendLine("    {");
        sb.Append("        moduleRegistry.Register(new ").Append(model.Name).Append("Module(")
            .Append(model.OptionsTypeExpression is null ? string.Empty : "options").AppendLine("));");
        sb.AppendLine("        return moduleRegistry;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Writes the other half of each partial service that declared no constructor.
    /// </summary>
    /// <param name="model">The facade whose services are written.</param>
    /// <returns>
    /// The generated source, or <c>null</c> when no service needs a part written for it.
    /// </returns>
    /// <remarks>
    /// The options field and the one line that assigns it. A service that wrote its own
    /// constructor gets nothing: no member is added to a type whose author already said how
    /// it is built.
    /// </remarks>
    private static string? EmitPluginServiceParts(PluginFacadeModel model)
    {
        if (model.OptionsTypeExpression is not { } optionsType)
        {
            return null;
        }

        StringBuilder? sb = null;

        foreach (var service in model.Services)
        {
            if (service.GeneratedTypeName is not { } typeName)
            {
                continue;
            }

            if (sb is null)
            {
                sb = new StringBuilder();
                sb.AppendLine("// <auto-generated/>");
                sb.AppendLine("#nullable enable");
            }

            sb.AppendLine();

            var indent = string.Empty;

            if (service.GeneratedNamespace is { } @namespace)
            {
                sb.Append("namespace ").AppendLine(@namespace);
                sb.AppendLine("{");
                indent = "    ";
            }

            sb.Append(indent).Append("[global::System.CodeDom.Compiler.GeneratedCode(\"Stella.Ergosfare.SourceGenerator\", \"")
                .Append(GeneratorVersion.Value).AppendLine("\")]");
            sb.Append(indent).Append("partial ").Append(service.GeneratedTypeKeyword).Append(' ')
                .AppendLine(typeName);
            sb.Append(indent).AppendLine("{");
            sb.Append(indent).AppendLine("    /// <summary>The options the consumer passed to this plugin's Add method.</summary>");
            sb.Append(indent).Append("    private readonly ").Append(optionsType).AppendLine(" _options;");
            sb.AppendLine();
            sb.Append(indent).Append("    public ").Append(typeName).Append('(').Append(optionsType)
                .AppendLine(" options)");
            sb.Append(indent).AppendLine("    {");
            sb.Append(indent).AppendLine("        _options = options;");
            sb.Append(indent).AppendLine("    }");
            sb.Append(indent).AppendLine("}");

            if (service.GeneratedNamespace is not null)
            {
                sb.AppendLine("}");
            }
        }

        return sb?.ToString();
    }
}
