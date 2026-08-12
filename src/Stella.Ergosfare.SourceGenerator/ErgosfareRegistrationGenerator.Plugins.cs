using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     The plugin facade half of the generator: an assembly that declares itself a plugin
///     with <c>[assembly: ErgosfarePlugin("Name")]</c> gets the module surface its consumers
///     call — an <c>IModule</c> implementation and an <c>Add&lt;Name&gt;</c> extension on the
///     module registry — emitted into its own compilation.
/// </summary>
/// <remarks>
///     <para>
///         This runs in the <b>plugin's</b> compilation, not the consumer's. The two halves
///         never observe each other: the facade is compiled into the plugin assembly and the
///         consumer calls it like any other extension method, while the consumer's own
///         generator reads the plugin's <c>[PipelineInvokable]</c> methods from metadata.
///         Nothing here depends on generator-to-generator visibility, which Roslyn does not
///         provide.
///     </para>
///     <para>
///         Emission is conditional on the declaration: an assembly without the attribute
///         gets no extra source at all.
///     </para>
/// </remarks>
public sealed partial class ErgosfareRegistrationGenerator
{
    private const string PluginsAbstractionsNamespace = "Stella.Ergosfare.Plugins.Abstractions";
    private const string ErgosfarePluginAttributeName = "ErgosfarePluginAttribute";
    private const string PipelineInvokableAttributeName = "PipelineInvokableAttribute";
    private const string VoidPipelineInvokableAttributeName = "VoidPipelineInvokableAttribute";

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

    /// <summary>
    ///     Mirrors <c>ExperimentalIds.PluginSurface</c>; spelled here because the generator
    ///     resolves nothing through a reference to either abstractions package.
    /// </summary>
    private const string PluginSurfaceExperimentalId = "ERGOEXP002";

    /// <summary>
    ///     Wires the plugin facade output. Kept as its own source output rather than folded
    ///     into the registration emission: the two have no shared inputs, and a plugin
    ///     assembly usually declares no dispatch sites of its own.
    /// </summary>
    private static void RegisterPluginFacade(IncrementalGeneratorInitializationContext context)
    {
        var facade = context.CompilationProvider.Select(static (compilation, ct) => ReadPluginFacade(compilation, ct));

        context.RegisterSourceOutput(facade, static (spc, model) =>
        {
            if (model is null)
            {
                return;
            }

            spc.AddSource("ErgosfarePluginFacade.g.cs", EmitPluginFacade(model.Value));
        });
    }

    private const string ErgosfareContextMetadataName = "Stella.Ergosfare.Core.Abstractions.ErgosfareContext";
    private const string PluginServiceFilterAttributeName = "PluginServiceFilterAttribute";
    private const string ValueTaskMetadataName = "System.Threading.Tasks.ValueTask";

    /// <summary>
    ///     Collects every <c>[PipelineInvokable]</c> / <c>[VoidPipelineInvokable]</c> method
    ///     visible to this compilation — declared here or carried by a scanned reference —
    ///     reduced to what emission needs.
    /// </summary>
    /// <remarks>
    ///     Reference scanning follows the same rules as the participant scan: an assembly
    ///     that does not reference Ergosfare cannot carry plugin methods, and one matching the
    ///     reserved prefix is skipped unless it force-opts in — the case ERGOSG015 reports so
    ///     the skip is never silent.
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

                if (!ReferencesErgosfare(assembly)
                    || (IsErgosfareAssemblyName(assembly.Name) && !HasForceScanReferencesOptIn(assembly)))
                {
                    continue;
                }

                CollectPluginInvocations(assembly.GlobalNamespace, context, valueTask, ref models, ct);
            }
        }

        return models?.ToImmutable() ?? ImmutableArray<PluginInvocationModel>.Empty;
    }

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
            || !IsNameableClosedType(type, type.ContainingAssembly))
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

            foreach (var attribute in method.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeClass
                    || !IsInNamespace(attributeClass, PluginsAbstractionsNamespace)
                    || attribute.ConstructorArguments.Length != 1
                    || attribute.ConstructorArguments[0].Value is not int stage)
                {
                    continue;
                }

                var shape = attributeClass.Name switch
                {
                    PipelineInvokableAttributeName => PluginPipelineShape.Result,
                    VoidPipelineInvokableAttributeName => PluginPipelineShape.Void,
                    _ => (PluginPipelineShape?)null,
                };

                if (shape is null || !Enum.IsDefined(typeof(PluginStage), stage))
                {
                    continue;
                }

                models ??= ImmutableArray.CreateBuilder<PluginInvocationModel>();
                models.Add(new PluginInvocationModel(
                    typeExpression,
                    displayName,
                    method.Name,
                    (PluginStage)stage,
                    shape.Value,
                    IsAsync: valueTask is not null
                             && SymbolEqualityComparer.Default.Equals(method.ReturnType.OriginalDefinition, valueTask),
                    method.IsStatic,
                    method.Arity,
                    filter.Modules,
                    filter.Keys,
                    BindParameters(method, context),
                    ReadConstraints(method),
                    Location: null));
            }
        }
    }

    /// <summary>
    ///     Reads the method's generic constraints — the design's shape filter. The call is
    ///     closed over each plan's concrete types at emission, so a constraint is both a
    ///     selection rule and a compilability requirement: a plan whose message fails it
    ///     would produce source the consumer's build rejects.
    /// </summary>
    /// <remarks>
    ///     Anything the string model cannot decide marks the whole method unmodelable rather
    ///     than being ignored. A constructed generic constraint is one such case: the message's
    ///     assignable chain is normalized to unbound definitions, so
    ///     <c>ICommand&lt;string&gt;</c> and <c>ICommand&lt;int&gt;</c> are indistinguishable
    ///     there, and admitting the call on that evidence is exactly the broken build the
    ///     conservative skip exists to prevent.
    /// </remarks>
    private static PluginConstraintModel ReadConstraints(IMethodSymbol method)
    {
        if (method.TypeParameters.IsEmpty)
        {
            return PluginConstraintModel.None;
        }

        // Only the message parameter's constraints are modeled. A constraint on the result
        // parameter would have to be checked against a result type the plan models as a bare
        // expression, with no assignable chain behind it.
        for (var i = 1; i < method.TypeParameters.Length; i++)
        {
            var other = method.TypeParameters[i];

            if (other.HasReferenceTypeConstraint || other.HasValueTypeConstraint
                || other.HasConstructorConstraint || other.HasUnmanagedTypeConstraint
                || !other.ConstraintTypes.IsEmpty)
            {
                return new PluginConstraintModel(ImmutableArray<string>.Empty, false, false, IsUnmodelable: true);
            }
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

            types.Add(NormalizedTypeExpression(named));
        }

        return new PluginConstraintModel(
            types.ToImmutable(),
            parameter.HasReferenceTypeConstraint,
            parameter.HasValueTypeConstraint,
            IsUnmodelable: false);
    }

    /// <summary>
    ///     Resolves each parameter to what emission substitutes for it: the plan's message and
    ///     result come from the method's own type parameters, the context from the plan's, and
    ///     anything else from the dispatching provider.
    /// </summary>
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
                ITypeParameterSymbol { Ordinal: 1 } p when p.DeclaringMethod is not null => PluginParameterKind.Result,
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

    /// <summary>The family and key filter carried by one attribute list, unfiltered when absent.</summary>
    private static (PluginModule Modules, ImmutableArray<string> Keys) ReadServiceFilter(
        ImmutableArray<AttributeData> attributes)
    {
        var modules = PluginModule.All;
        var keys = ImmutableArray<string>.Empty;

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { Name: PluginServiceFilterAttributeName } attributeClass
                || !IsInNamespace(attributeClass, PluginsAbstractionsNamespace)
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
    ///     A method-level filter narrows the type-level one rather than replacing it: families
    ///     intersect, and keys concatenate because each named key is an additional plan set the
    ///     plugin opts into.
    /// </summary>
    private static (PluginModule Modules, ImmutableArray<string> Keys) Intersect(
        (PluginModule Modules, ImmutableArray<string> Keys) type,
        (PluginModule Modules, ImmutableArray<string> Keys) method)
        => (type.Modules & method.Modules, type.Keys.IsEmpty ? method.Keys : type.Keys.AddRange(method.Keys));

    /// <summary>
    ///     Reports plugin packages the reserved-prefix rule silently excludes from reference
    ///     scanning. The exclusion is right for the library's own assemblies — their
    ///     marker-inheriting contract interfaces must stay out of generated registrations —
    ///     but a plugin named under the same prefix becomes a no-op with nothing said, which
    ///     is the worst failure mode a plugin ecosystem can have.
    /// </summary>
    /// <remarks>
    ///     The gate is the assembly's own <c>[ErgosfarePlugin]</c> declaration rather than a
    ///     type walk: an assembly that never declared itself a plugin is not one being
    ///     silently dropped, and the check stays O(1) per reference.
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

    private static ImmutableArray<string> FindPrefixExcludedPlugins(Compilation compilation, CancellationToken ct)
    {
        ImmutableArray<string>.Builder? excluded = null;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            ct.ThrowIfCancellationRequested();

            if (!IsErgosfareAssemblyName(assembly.Name)
                || HasForceScanReferencesOptIn(assembly)
                || ReadPluginName(assembly) is null)
            {
                continue;
            }

            excluded ??= ImmutableArray.CreateBuilder<string>();
            excluded.Add(assembly.Name);
        }

        return excluded?.ToImmutable() ?? ImmutableArray<string>.Empty;
    }

    /// <summary>
    ///     Reads the plugin declaration and the services behind it: every accessible type in
    ///     this compilation carrying at least one <c>[PipelineInvokable]</c> method. Returns
    ///     <c>null</c> when the assembly is not a plugin, or when the DI module surface the
    ///     facade implements is not referenced — the same shape the module builders' own
    ///     availability gate uses.
    /// </summary>
    private static PluginFacadeModel? ReadPluginFacade(Compilation compilation, CancellationToken ct)
    {
        var name = ReadPluginName(compilation.Assembly);

        if (name is null || compilation.GetTypeByMetadataName(ModuleMetadataName) is null)
        {
            return null;
        }

        var services = ImmutableArray.CreateBuilder<string>();

        CollectPluginServices(compilation.Assembly.GlobalNamespace, services, ct);

        var ordered = services.ToImmutable().Sort(StringComparer.Ordinal);

        return new PluginFacadeModel(name, ordered);
    }

    /// <summary>The <c>[assembly: ErgosfarePlugin("…")]</c> name, or <c>null</c>.</summary>
    private static string? ReadPluginName(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: ErgosfarePluginAttributeName } attributeClass
                && IsInNamespace(attributeClass, PluginsAbstractionsNamespace)
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is string name
                && name.Length > 0)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    ///     Walks the assembly for types declaring <c>[PipelineInvokable]</c> methods. A type
    ///     the generated facade cannot name — private nested, file-local — is skipped here;
    ///     the consumer-side scan reports it, so the facade does not need its own diagnostic.
    /// </summary>
    private static void CollectPluginServices(
        INamespaceSymbol @namespace,
        ImmutableArray<string>.Builder services,
        CancellationToken ct)
    {
        foreach (var member in @namespace.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            switch (member)
            {
                case INamespaceSymbol nested:
                    CollectPluginServices(nested, services, ct);
                    break;

                case INamedTypeSymbol type:
                    CollectPluginServices(type, services, ct);
                    break;
            }
        }
    }

    private static void CollectPluginServices(
        INamedTypeSymbol type,
        ImmutableArray<string>.Builder services,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var nested in type.GetTypeMembers())
        {
            CollectPluginServices(nested, services, ct);
        }

        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false }
            || type.IsGenericType
            || !IsNameableClosedType(type, type.ContainingAssembly))
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method && HasPipelineInvokable(method))
            {
                services.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                return;
            }
        }
    }

    /// <summary>
    ///     Whether the method carries either invokable attribute. The result-typed and
    ///     resultless shapes are separate declarations because their signatures genuinely
    ///     differ, but for "does this type need registering" they are the same question.
    /// </summary>
    private static bool HasPipelineInvokable(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass is
                    { Name: PipelineInvokableAttributeName or VoidPipelineInvokableAttributeName } attributeClass
                && IsInNamespace(attributeClass, PluginsAbstractionsNamespace))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Emits the module and its registry extension. Services are registered as
    ///     singletons: a plugin service is resolved once per container and lives as long as
    ///     the composition it is baked into, so per-dispatch resolution never happens. Its
    ///     per-dispatch dependencies arrive as method parameters instead, which is why the
    ///     lifetime is fixed here rather than declared.
    /// </summary>
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
            .Append(GeneratorVersion).AppendLine("\")]");
        sb.Append("internal sealed class ").Append(model.Name).Append("Module : ")
            .Append(ModuleInterfaceExpression).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public void Build(").Append(ModuleConfigurationExpression).AppendLine(" configuration)");
        sb.AppendLine("    {");

        if (model.ServiceTypeExpressions.IsEmpty)
        {
            sb.AppendLine("        // The plugin declares no [PipelineInvokable] methods yet — nothing to register.");
        }
        else
        {
            foreach (var service in model.ServiceTypeExpressions)
            {
                sb.Append("        ").Append(ServiceCollectionDescriptorExtensionsExpression)
                    .Append(".TryAddSingleton<").Append(service).AppendLine(">(configuration.Services);");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>Adds this plugin to an Ergosfare module registry.</summary>");
        sb.Append("[global::System.CodeDom.Compiler.GeneratedCode(\"Stella.Ergosfare.SourceGenerator\", \"")
            .Append(GeneratorVersion).AppendLine("\")]");
        // The consumer's opt-in point: installing a plugin is using the experimental surface,
        // and this extension is the one line of it they write themselves.
        sb.Append("[global::System.Diagnostics.CodeAnalysis.Experimental(\"")
            .Append(PluginSurfaceExperimentalId).AppendLine("\")]");
        sb.Append("public static class ").Append(model.Name).AppendLine("PluginModuleRegistryExtensions");
        sb.AppendLine("{");
        sb.Append("    public static ").Append(ModuleRegistryExpression)
            .Append(" Add").Append(model.Name).Append("(this ").Append(ModuleRegistryExpression)
            .AppendLine(" moduleRegistry)");
        sb.AppendLine("    {");
        sb.Append("        moduleRegistry.Register(new ").Append(model.Name).AppendLine("Module());");
        sb.AppendLine("        return moduleRegistry;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>The plugin declaration reduced to what the facade emission needs.</summary>
    private readonly record struct PluginFacadeModel(string Name, ImmutableArray<string> ServiceTypeExpressions)
    {
        public bool Equals(PluginFacadeModel other)
            => Name == other.Name
               && ServiceTypeExpressions.SequenceEqualOrBothEmpty(other.ServiceTypeExpressions);

        public override int GetHashCode()
            => (Name.GetHashCode() * 397) ^ ServiceTypeExpressions.Length;
    }
}
