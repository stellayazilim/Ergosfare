namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     ERGO016 — the diagnostic for a participant that registers and then binds to
///     nothing. Participants are matched to messages by concrete type; a generic
///     participant's contract names a type parameter, so every message's stage arrays come
///     out without it. Before this diagnostic that happened in complete silence, which for
///     a validation or authorization interceptor is a bypass nobody is told about.
/// </summary>
public class GenericParticipantDiagnosticTests
{
    /// <summary>
    ///     A participant whose constraint no compiled message satisfies. Monomorphization
    ///     closes an open participant over every message its constraint admits, so the only
    ///     way one still binds to nothing is for that set to be empty — which is exactly
    ///     when ERGO016 has something to say.
    /// </summary>
    private const string GenericInterceptor = """
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Commands.Abstractions;
        using System.Threading.Tasks;

        namespace TestApp
        {
            public sealed class Ship : ICommand { }

            public sealed class ShipHandler : ICommandHandler<Ship>
            {
                public ValueTask HandleAsync(Ship command, ErgosfareContext context) => default;
            }

            public interface IAudited : ICommand { }

            public sealed class ValidateCommands<TCommand> : ICommandPreInterceptor<TCommand>
                where TCommand : IAudited
            {
                public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
                    => ValueTask.FromResult(command);
            }
        }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void GenericParticipant_ReportsErgosg016()
    {
        var result = GeneratorTestHost.Run(GenericInterceptor);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO016");
    }

    /// <summary>
    ///     The diagnostic states the truth the emitted code shows: the participant is
    ///     registered, and no message's pipeline contains it. Pinning both together is what
    ///     keeps the diagnostic honest — the day this participant binds, this test says so.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenericParticipant_IsAbsentFromEveryMessagePipeline()
    {
        var result = GeneratorTestHost.Run(GenericInterceptor);

        Assert.Empty(result.CompilationErrors);

        // Registered — the selection and participant list both name it...
        Assert.Contains("typeof(global::TestApp.ValidateCommands<>)", result.GeneratedSource);

        // ...and yet no frozen composition names it, which is the whole finding.
        var compositions = result.GeneratedSource
            .Split("AddFrozenComposition")
            .Skip(1);

        Assert.All(compositions, composition =>
            Assert.DoesNotContain("ValidateCommands", composition));
    }

    /// <summary>
    ///     A non-generic interceptor over the same command is the control: it binds, so it
    ///     draws no diagnostic and does appear in the pipeline.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void NonGenericParticipant_BindsAndReportsNothing()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed class Ship : ICommand { }

                public sealed class ShipHandler : ICommandHandler<Ship>
                {
                    public ValueTask HandleAsync(Ship command, ErgosfareContext context) => default;
                }

                public sealed class ValidateShip : ICommandPreInterceptor<Ship>
                {
                    public ValueTask<Ship> HandleAsync(Ship command, ErgosfareContext context)
                        => ValueTask.FromResult(command);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO016");

        var composition = result.GeneratedSource
            .Split("AddFrozenComposition")
            .Skip(1)
            .Single();

        Assert.Contains("ValidateShip", composition);
    }

    /// <summary>
    ///     The generic shape that does work, and must keep working: a participant for a
    ///     generic message, its contract built from its own type parameters. The table keys
    ///     that message by its definition and the dispatch closes the participant over the
    ///     message's own arguments, so it binds — and must draw no diagnostic. This is the
    ///     line between the two generic shapes, and the reason ERGO016 is narrow.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenericParticipantForAGenericMessage_BindsAndReportsNothing()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed class Wrap<T> : ICommand { public T? Value; }

                public sealed class WrapHandler<T> : ICommandHandler<Wrap<T>>
                {
                    public ValueTask HandleAsync(Wrap<T> command, ErgosfareContext context) => default;
                }

                public sealed class WrapPre<T> : ICommandPreInterceptor<Wrap<T>>
                {
                    public ValueTask<Wrap<T>> HandleAsync(Wrap<T> command, ErgosfareContext context)
                        => ValueTask.FromResult(command);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO016");

        // And it really does bind: the message's own composition names both participants.
        var composition = result.GeneratedSource
            .Split("AddFrozenComposition")
            .Skip(1)
            .Single();

        Assert.Contains("WrapHandler", composition);
        Assert.Contains("WrapPre", composition);
    }

    /// <summary>
    ///     A generic type carrying no participant contract is not a participant, so it is
    ///     none of this diagnostic's business.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GenericNonParticipant_ReportsNothing()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed class Ship : ICommand { }

                public sealed class ShipHandler : ICommandHandler<Ship>
                {
                    public ValueTask HandleAsync(Ship command, ErgosfareContext context) => default;
                }

                public sealed class Box<T> { public T? Value { get; set; } }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGO016");
    }
}
