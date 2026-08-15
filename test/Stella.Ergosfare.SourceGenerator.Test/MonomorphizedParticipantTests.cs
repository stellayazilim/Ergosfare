namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Monomorphization: a participant that takes its message as a type parameter is closed
///     over each message its constraint admits, and each closed form is a distinct type with
///     its own registration and its own place in that message's pipeline — the compile-time
///     counterpart of the instantiations a generic method gets in the binary.
/// </summary>
/// <remarks>
///     The closing set does not come from the source. Nobody writes
///     <c>ValidateCommands&lt;RegisterUser&gt;</c>; the set is the type parameter's
///     constraint intersected with the compiled message set.
/// </remarks>
public class MonomorphizedParticipantTests
{
    private const string TwoCommandsOneOpenInterceptor = """
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Queries.Abstractions;
        using System.Threading.Tasks;

        namespace TestApp
        {
            public sealed class RegisterUser : ICommand { }

            public sealed class RegisterUserHandler : ICommandHandler<RegisterUser>
            {
                public ValueTask HandleAsync(RegisterUser command, ErgosfareContext context) => default;
            }

            public sealed class DeleteUser : ICommand { }

            public sealed class DeleteUserHandler : ICommandHandler<DeleteUser>
            {
                public ValueTask HandleAsync(DeleteUser command, ErgosfareContext context) => default;
            }

            public sealed class ValidateCommands<TCommand> : ICommandPreInterceptor<TCommand>
                where TCommand : ICommand
            {
                public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
                    => ValueTask.FromResult(command);
            }
        }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void OpenParticipant_IsClosedOverEveryMessageItsConstraintAdmits()
    {
        var result = GeneratorTestHost.Run(TwoCommandsOneOpenInterceptor);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("global::TestApp.ValidateCommands<global::TestApp.RegisterUser>", result.GeneratedSource);
        Assert.Contains("global::TestApp.ValidateCommands<global::TestApp.DeleteUser>", result.GeneratedSource);
    }

    /// <summary>
    ///     The point of the whole exercise: the closed form is in the message's pipeline, so
    ///     it actually runs. Before monomorphization the message's composition came out with
    ///     every interceptor segment empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void EachClosedForm_LandsInItsOwnMessagePipeline()
    {
        var result = GeneratorTestHost.Run(TwoCommandsOneOpenInterceptor);

        Assert.Empty(result.CompilationErrors);

        var compositions = result.GeneratedSource
            .Split("AddFrozenComposition", StringSplitOptions.None)
            .Skip(1)
            .ToList();

        var registerUser = Assert.Single(compositions, c => c.Contains("typeof(global::TestApp.RegisterUser)"));
        var deleteUser = Assert.Single(compositions, c => c.Contains("typeof(global::TestApp.DeleteUser)"));

        // Each message gets its own instantiation, and only its own.
        Assert.Contains("ValidateCommands<global::TestApp.RegisterUser>", registerUser);
        Assert.DoesNotContain("ValidateCommands<global::TestApp.DeleteUser>", registerUser);

        Assert.Contains("ValidateCommands<global::TestApp.DeleteUser>", deleteUser);
        Assert.DoesNotContain("ValidateCommands<global::TestApp.RegisterUser>", deleteUser);
    }

    /// <summary>
    ///     Once a participant closes over something, it is no longer the shape ERGO016
    ///     reports — that diagnostic is for a participant that runs for no message at all.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MonomorphizedParticipant_NoLongerReportsErgosg016()
    {
        var result = GeneratorTestHost.Run(TwoCommandsOneOpenInterceptor);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO016");
    }

    /// <summary>
    ///     The constraint is the filter, not the marker: a command-constrained interceptor
    ///     does not close over a query.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TheConstraint_DecidesWhichMessagesCloseIt()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed class RegisterUser : ICommand { }

                public sealed class RegisterUserHandler : ICommandHandler<RegisterUser>
                {
                    public ValueTask HandleAsync(RegisterUser command, ErgosfareContext context) => default;
                }

                public sealed class FindUser : IQuery<int> { }

                public sealed class FindUserHandler : IQueryHandler<FindUser, int>
                {
                    public ValueTask<int> HandleAsync(FindUser query, ErgosfareContext context)
                        => ValueTask.FromResult(1);
                }

                public sealed class ValidateCommands<TCommand> : ICommandPreInterceptor<TCommand>
                    where TCommand : ICommand
                {
                    public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
                        => ValueTask.FromResult(command);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("ValidateCommands<global::TestApp.RegisterUser>", result.GeneratedSource);
        Assert.DoesNotContain("ValidateCommands<global::TestApp.FindUser>", result.GeneratedSource);
    }

    /// <summary>
    ///     A participant whose constraint no message satisfies closes over nothing, so it
    ///     keeps ERGO016: it was registered and it still runs for no message.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ParticipantThatClosesOverNothing_KeepsErgosg016()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface INeverImplemented : ICommand { }

                public sealed class ValidateCommands<TCommand> : ICommandPreInterceptor<TCommand>
                    where TCommand : INeverImplemented
                {
                    public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
                        => ValueTask.FromResult(command);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO016");
    }
}
