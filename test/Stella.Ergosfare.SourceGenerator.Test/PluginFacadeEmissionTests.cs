using System.Linq;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     The plugin facade: an assembly declaring itself a plugin gets the module surface its
///     consumers call, emitted into its own compilation. An assembly that declares nothing
///     gets no extra source at all — which is also what keeps every other generator test's
///     single-tree assumption true.
/// </summary>
public class PluginFacadeEmissionTests
{
    private const string FacadeHintName = "ErgosfarePluginFacade.g.cs";
    private const string ServicePartsHintName = "ErgosfarePluginServices.g.cs";

    /// <summary>The facade source, or <c>null</c> when the generator emitted none.</summary>
    private static string? Facade(GeneratorTestHost.GeneratorRunResult result)
        => Emitted(result, FacadeHintName);

    /// <summary>The generated service halves, or <c>null</c> when no service needed one.</summary>
    private static string? ServiceParts(GeneratorTestHost.GeneratorRunResult result)
        => Emitted(result, ServicePartsHintName);

    private static string? Emitted(GeneratorTestHost.GeneratorRunResult result, string hintName)
        => result.DriverResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(s => s.HintName == hintName)
            .Select(s => s.SourceText.ToString())
            .FirstOrDefault();

    [Fact]
    public void EmptyPluginEmitsModuleAndRegistryExtension()
    {
        const string source = """
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing")]

            namespace PluginUnderTest;

            public sealed class Nothing;
            """;

        var result = GeneratorTestHost.Run(source);

        var facade = Facade(result);

        Assert.NotNull(facade);

        // The IModule implementation the consumer's registry ends up holding.
        Assert.Contains(
            "internal sealed class TracingModule : global::Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModule",
            facade);
        Assert.Contains("public void Build(", facade);

        // The entry point the consumer calls: AddErgosfare(o => o.AddTracing()).
        Assert.Contains("public static class TracingPluginModuleRegistryExtensions", facade);
        Assert.Contains("AddTracing(this global::Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModuleRegistry", facade);
        Assert.Contains("moduleRegistry.Register(new TracingModule());", facade);

        // An empty plugin registers nothing, and says so rather than emitting an empty body.
        Assert.Contains("no [PipelineInvokable] methods yet", facade);

        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void PluginServiceCarryingAnInvokableIsRegisteredAsSingleton()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing")]

            namespace PluginUnderTest;

            public sealed class TracingHooks
            {
                [PipelineInvokable(Hook.PostMain)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var facade = Facade(result);

        Assert.NotNull(facade);

        // The lifetime is fixed, not declared: the service lives as long as the composition
        // it is baked into, so a dispatch never resolves it.
        Assert.Contains("TryAddSingleton<global::PluginUnderTest.TracingHooks>(configuration.Services)", facade);

        Assert.Empty(result.CompilationErrors);
    }

    /// <summary>
    ///     The registry extension is named after the plugin's own declaration, so installing
    ///     one reads as <c>AddCounting()</c> and two plugins never collide.
    /// </summary>
    [Fact]
    public void PluginRegistryExtensionIsNamedAfterTheDeclaration()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Counting")]

            namespace PluginUnderTest;

            public sealed class CountingHooks
            {
                [PipelineInvokable(Hook.PostMain)]
                public void Count<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var facade = Facade(result);

        Assert.NotNull(facade);
        Assert.Contains("TryAddSingleton<global::PluginUnderTest.CountingHooks>(configuration.Services)", facade);
        Assert.Contains("AddCounting(this ", facade);

        Assert.Empty(result.CompilationErrors);
    }

    [Fact]
    public void AssemblyWithoutThePluginDeclarationEmitsNoFacade()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            namespace PluginUnderTest;

            public sealed class TracingHooks
            {
                [PipelineInvokable(Hook.PostMain)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Null(Facade(result));
    }

    /// <summary>
    ///     A plugin declaring options: the consumer's <c>AddTracing</c> takes one, the module
    ///     holds it, and the service is constructed with it. The instance is never registered,
    ///     so the container carries nothing for a type it has no reason to know about.
    /// </summary>
    [Fact]
    public void DeclaredOptions_AreHeldByTheModuleAndNeverEnterTheContainer()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing", typeof(PluginUnderTest.TracingOptions))]

            namespace PluginUnderTest;

            public sealed class TracingOptions
            {
                public double SampleRate { get; init; }
            }

            public sealed class TracingHooks(TracingOptions options)
            {
                [PipelineInvokable(Hook.Start)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var facade = Facade(result);

        Assert.NotNull(facade);

        // The consumer constructs the instance and passes it; nothing sits in between.
        Assert.Contains("AddTracing(this global::Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.IModuleRegistry moduleRegistry, global::PluginUnderTest.TracingOptions options)", facade);
        Assert.Contains("moduleRegistry.Register(new TracingModule(options));", facade);
        Assert.Contains("private readonly global::PluginUnderTest.TracingOptions _options;", facade);

        // Constructed with the module's own instance — no resolution of the options type.
        Assert.Contains("serviceProvider => new global::PluginUnderTest.TracingHooks(_options)", facade);
        Assert.DoesNotContain("GetRequiredService<global::PluginUnderTest.TracingOptions>", facade);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    /// <summary>
    ///     The author's constructor is the contract: its parameters bind the way a hook
    ///     method's do — the options type from the module's instance, everything else from
    ///     the container. Which is what lets a plugin service take ordinary dependencies.
    /// </summary>
    [Fact]
    public void AuthoredConstructor_BindsOptionsAndResolvesTheRest()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing", typeof(PluginUnderTest.TracingOptions))]

            namespace PluginUnderTest;

            public interface IClock;

            public sealed class TracingOptions;

            public sealed class TracingHooks(IClock clock, TracingOptions options)
            {
                [PipelineInvokable(Hook.Start)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var facade = Facade(result);

        Assert.NotNull(facade);
        Assert.Contains(
            "serviceProvider => new global::PluginUnderTest.TracingHooks("
            + "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions"
            + ".GetRequiredService<global::PluginUnderTest.IClock>(serviceProvider), _options)",
            facade);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    /// <summary>
    ///     The implicit path: a partial service that wrote no constructor gets the field and
    ///     the one line that assigns it, so the author writes nothing at all.
    /// </summary>
    [Fact]
    public void PartialServiceWithoutAConstructor_GetsTheFieldWritten()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing", typeof(PluginUnderTest.TracingOptions))]

            namespace PluginUnderTest;

            public sealed class TracingOptions;

            public sealed partial class TracingHooks
            {
                [PipelineInvokable(Hook.Start)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        // The other half lands in the service's own namespace, so it is its own file.
        var parts = ServiceParts(result);

        Assert.NotNull(parts);
        Assert.Contains("namespace PluginUnderTest", parts);
        Assert.Contains("partial class TracingHooks", parts);
        Assert.Contains("private readonly global::PluginUnderTest.TracingOptions _options;", parts);
        Assert.Contains("public TracingHooks(global::PluginUnderTest.TracingOptions options)", parts);

        Assert.Contains("serviceProvider => new global::PluginUnderTest.TracingHooks(_options)", Facade(result));

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }

    /// <summary>
    ///     No constructor and not partial: the generator has no way in, and says so. A plugin
    ///     whose author declared settings and whose service silently ignores them is the
    ///     failure mode this surface exists to avoid.
    /// </summary>
    [Fact]
    public void ServiceThatCannotReceiveOptions_IsReported()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing", typeof(PluginUnderTest.TracingOptions))]

            namespace PluginUnderTest;

            public sealed class TracingOptions;

            public sealed class TracingHooks
            {
                [PipelineInvokable(Hook.Start)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);

        Assert.Equal("ERGO017", diagnostic.Id);
        Assert.Contains("TracingOptions", diagnostic.GetMessage());

        // Still registered — the plugin works, it just never sees the settings.
        Assert.Contains("TryAddSingleton<global::PluginUnderTest.TracingHooks>", Facade(result));

        Assert.Empty(result.CompilationErrors);
    }

    /// <summary>
    ///     A plugin that declares no options is untouched by any of this: the extension stays
    ///     parameterless and the container activates the service, exactly as before.
    /// </summary>
    [Fact]
    public void WithoutDeclaredOptions_TheFacadeIsUnchanged()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Tracing")]

            namespace PluginUnderTest;

            public sealed class TracingHooks
            {
                [PipelineInvokable(Hook.Start)]
                public void Observe<TMessage>(TMessage message, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        var facade = Facade(result);

        Assert.NotNull(facade);
        Assert.Contains("moduleRegistry.Register(new TracingModule());", facade);
        Assert.Contains("TryAddSingleton<global::PluginUnderTest.TracingHooks>(configuration.Services);", facade);
        Assert.DoesNotContain("_options", facade);
        Assert.Null(ServiceParts(result));

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
    }
}
