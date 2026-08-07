namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// The descriptor-catalog module initializer: every modeled handler type contributes its
/// compile-time descriptor factory, plain messages contribute nothing, and the emitted
/// initializer compiles — from then on a manual <c>Register&lt;THandler&gt;()</c> in the
/// compilation registers without reflection.
/// </summary>
public class DescriptorCatalogEmissionTests
{
    [Fact]
    public void HandlerTypes_ContributeTheirDescriptorFactories()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record CatalogPing : ICommand;

                public sealed class CatalogPingHandler : ICommandHandler<CatalogPing>
                {
                    public ValueTask HandleAsync(CatalogPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", result.GeneratedSource);
        Assert.Contains(
            "GeneratedDescriptorCatalog.Add(typeof(global::TestApp.CatalogPingHandler), static () =>",
            result.GeneratedSource);

        // The message type itself carries no handler contracts — no catalog entry.
        Assert.DoesNotContain("GeneratedDescriptorCatalog.Add(typeof(global::TestApp.CatalogPing),", result.GeneratedSource);
    }
}
