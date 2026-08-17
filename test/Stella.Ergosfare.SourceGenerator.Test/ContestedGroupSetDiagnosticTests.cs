using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// A group set that selects two main handlers for one message. A send delivers to one
/// handler, so the set cannot be satisfied and every dispatch under it throws — which the
/// build says, instead of the planner dropping the plan without a word and the dispatch
/// finding out at run time.
/// </summary>
public class ContestedGroupSetDiagnosticTests
{
    private const string TwoGroupedHandlers = """
        using System;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;

        namespace TestApp
        {
            public sealed record LoginCommand : ICommand;

            [Group("User")]
            public sealed class UserLoginHandler : ICommandHandler<LoginCommand>
            {
                public ValueTask HandleAsync(LoginCommand command, ErgosfareContext context) => default;
            }

            [Group("Admin")]
            public sealed class DashboardLoginHandler : ICommandHandler<LoginCommand>
            {
                public ValueTask HandleAsync(LoginCommand command, ErgosfareContext context) => default;
            }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void AGroupSetSelectingTwoHandlers_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(TwoGroupedHandlers + """

            public static class Caller
            {
                public static async Task Run(ICommandMediator mediator)
                    => await mediator.SendAsync(new LoginCommand(), new[] { "User", "Admin" });
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGO023");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

        var message = diagnostic.GetMessage();
        Assert.Contains("UserLoginHandler", message);
        Assert.Contains("DashboardLoginHandler", message);
        // The set is reported in its canonical order, so the names are asserted rather than
        // the string that joins them.
        Assert.Contains("User", message);
        Assert.Contains("Admin", message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AGroupSetSelectingOneHandler_IsPlannedAsUsual()
    {
        var result = GeneratorTestHost.Run(TwoGroupedHandlers + """

            public static class Caller
            {
                public static async Task Run(ICommandMediator mediator)
                    => await mediator.SendAsync(new LoginCommand(), new[] { "User" });
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        // The same two handlers, and no finding: the set names one of them, which is what a
        // group set is for.
        Assert.Empty(result.GeneratorDiagnostics.Where(d => d.Id == "ERGO023"));
    }
}
