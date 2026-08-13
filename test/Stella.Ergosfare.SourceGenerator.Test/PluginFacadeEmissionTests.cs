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

    /// <summary>The facade source, or <c>null</c> when the generator emitted none.</summary>
    private static string? Facade(GeneratorTestHost.GeneratorRunResult result)
        => result.DriverResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Where(s => s.HintName == FacadeHintName)
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
                [PipelineInvokable(Stage.PostMainHandler)]
                public void Observe<TMessage, TResult>(TMessage message, TResult result, ErgosfareContext context)
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
    ///     The resultless shape is a separate declaration but the same question for
    ///     registration: a plugin whose only method is void-shaped is still a service.
    /// </summary>
    [Fact]
    public void PluginServiceCarryingOnlyAVoidInvokableIsRegistered()
    {
        const string source = """
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;

            [assembly: ErgosfarePlugin("Counting")]

            namespace PluginUnderTest;

            public sealed class CountingHooks
            {
                [VoidPipelineInvokable(Stage.PostMainHandler)]
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
                [PipelineInvokable(Stage.PostMainHandler)]
                public void Observe<TMessage, TResult>(TMessage message, TResult result, ErgosfareContext context)
                {
                }
            }
            """;

        var result = GeneratorTestHost.Run(source);

        Assert.Null(Facade(result));
    }
}
