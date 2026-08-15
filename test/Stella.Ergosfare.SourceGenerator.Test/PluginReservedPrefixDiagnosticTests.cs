using System.Linq;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     ERGO015: a plugin package named under the reserved <c>Stella.Ergosfare</c> prefix is
///     excluded from reference scanning like the library's own assemblies, which turns it
///     into a silent no-op. The exclusion stays; the silence does not.
/// </summary>
public class PluginReservedPrefixDiagnosticTests
{
    private const string AppSource = """
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using System.Threading.Tasks;

        namespace App;

        public sealed record Ping : ICommand;

        public sealed class PingHandler : ICommandHandler<Ping>
        {
            public ValueTask HandleAsync(Ping message, ErgosfareContext context) => default;
        }
        """;

    private const string PluginSource = """
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Plugins.Abstractions;

        [assembly: ErgosfarePlugin("Tracing")]

        namespace TracingPlugin;

        public sealed class TracingHooks
        {
            [PipelineInvokable(Hook.PostMain)]
            public void Observe<TMessage>(TMessage message, ErgosfareContext context)
            {
            }
        }
        """;

    private const string OptedInPluginSource = """
        using System.Reflection;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Plugins.Abstractions;

        [assembly: ErgosfarePlugin("Tracing")]
        [assembly: AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")]

        namespace TracingPlugin;

        public sealed class TracingHooks
        {
            [PipelineInvokable(Hook.PostMain)]
            public void Observe<TMessage>(TMessage message, ErgosfareContext context)
            {
            }
        }
        """;

    private static bool HasErgosg015(GeneratorTestHost.GeneratorRunResult result)
        => result.GeneratorDiagnostics.Any(d => d.Id == "ERGO015");

    [Fact]
    public void PluginUnderTheReservedPrefix_WithoutOptIn_IsReported()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Stella.Ergosfare.Plugins.Tracing", PluginSource)]);

        Assert.True(HasErgosg015(result));

        var diagnostic = result.GeneratorDiagnostics.Single(d => d.Id == "ERGO015");

        Assert.Contains("Stella.Ergosfare.Plugins.Tracing", diagnostic.GetMessage());
        Assert.Contains("ErgosfareSourceGeneratorForceScanReferences", diagnostic.GetMessage());
    }

    [Fact]
    public void PluginUnderTheReservedPrefix_WithOptIn_IsNotReported()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Stella.Ergosfare.Plugins.Tracing2", OptedInPluginSource)]);

        Assert.False(HasErgosg015(result));
    }

    [Fact]
    public void PluginOutsideTheReservedPrefix_IsNotReported()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Contoso.Ergosfare.Tracing", PluginSource)]);

        Assert.False(HasErgosg015(result));
    }

    /// <summary>
    ///     The library's own prefixed assemblies never declare themselves plugins, so the
    ///     ordinary case stays quiet — the diagnostic keys on the declaration, not on the
    ///     name alone.
    /// </summary>
    [Fact]
    public void NonPluginAssemblyUnderTheReservedPrefix_IsNotReported()
    {
        var result = GeneratorTestHost.Run(AppSource);

        Assert.False(HasErgosg015(result));
    }
}
