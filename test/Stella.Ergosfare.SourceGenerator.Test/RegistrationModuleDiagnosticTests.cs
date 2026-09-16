namespace Stella.Ergosfare.SourceGenerator.Test;

public class RegistrationModuleDiagnosticTests
{
    [Theory]
    [InlineData("Commands", "Command", "Queries", "IQuery")]
    [InlineData("Queries", "Query", "Commands", "ICommand")]
    [InlineData("Events", "Event", "Commands", "ICommandHandler<Command>")]
    public void WrongModule_IsRejectedAtTheSelectionCall(string module, string builder, string contractModule, string contract)
    {
        var result = GeneratorTestHost.Run($$"""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.{{contractModule}}.Abstractions;
            public sealed record Command : ICommand;
            public sealed class Wrong : {{contract}} {
                public ValueTask HandleAsync(Command command, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context) => default;
            }
            public static class Startup {
                public static void Configure(Stella.Ergosfare.{{module}}.Extensions.MicrosoftDependencyInjection.{{builder}}ModuleBuilder builder)
                    => builder.Register(typeof(Wrong));
            }
            """);
        var error = Assert.Single(result.GeneratorDiagnostics.Where(d => d.Id == "ERGO025"));
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Wrong", error.GetMessage());
        Assert.True(error.Location.IsInSource);
    }

    [Fact]
    public void MarkerlessEvent_RemainsAValidExplicitSelection()
    {
        var result = GeneratorTestHost.Run("""
            public sealed record Notice;
            public static class Startup {
                public static void Configure(Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder builder)
                    => builder.Register<Notice>();
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO025");
    }
}
