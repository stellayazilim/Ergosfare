namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Frozen composition emission: every exactly-modelable default-discovery message gets an
/// <c>AddFrozenComposition</c> table entry carrying all six stages as pre-sorted
/// direct/indirect segment pairs — weight descending then ordinal CLR <c>FullName</c>,
/// the runtime shape-builder's comparator — with group labels baked per row and events
/// carrying every subscriber. Anything unmodelable (a keyed participant, a
/// pipeline-excluded message) stays off the table.
/// </summary>
public class FrozenCompositionEmissionTests
{
    [Fact]
    public void InterceptedCommand_BakesEverySegmentInRuntimeOrder()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public interface IAudited : ICommand;

                public sealed record FrozenPing : IAudited;

                public sealed class FrozenPingHandler : ICommandHandler<FrozenPing>
                {
                    public ValueTask HandleAsync(FrozenPing message, ErgosfareContext context) => default;
                }

                // Direct segment: ZetaPre loses to HeavyPre on weight, and to AlphaPre on
                // ordinal name where weights tie.
                public sealed class ZetaPre : ICommandPreInterceptor<FrozenPing>
                {
                    public ValueTask<FrozenPing> HandleAsync(FrozenPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                public sealed class AlphaPre : ICommandPreInterceptor<FrozenPing>
                {
                    public ValueTask<FrozenPing> HandleAsync(FrozenPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                [Weight(5)]
                public sealed class HeavyPre : ICommandPreInterceptor<FrozenPing>
                {
                    public ValueTask<FrozenPing> HandleAsync(FrozenPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                // Indirect segment: registered against the implemented interface.
                public sealed class BroadPre : ICommandPreInterceptor<IAudited>
                {
                    public ValueTask<IAudited> HandleAsync(IAudited message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                // A grouped participant keeps its label baked on the row.
                [Group("reporting")]
                public sealed class ReportingFinal : ICommandFinalInterceptor<FrozenPing>
                {
                    public ValueTask HandleAsync(FrozenPing message, object? messageResult, System.Exception? exception, ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;
        Assert.Contains("AddFrozenComposition(new global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenComposition(", source);
        Assert.Contains("typeof(global::TestApp.FrozenPing)", source);

        // The direct pre segment in the exact runtime order: weight first, then name.
        var heavy = source.IndexOf("new global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenParticipant(typeof(global::TestApp.HeavyPre))", StringComparison.Ordinal);
        var alpha = source.IndexOf("new global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenParticipant(typeof(global::TestApp.AlphaPre))", StringComparison.Ordinal);
        var zeta = source.IndexOf("new global::Stella.Ergosfare.Core.Abstractions.DispatchRoots.FrozenParticipant(typeof(global::TestApp.ZetaPre))", StringComparison.Ordinal);
        Assert.True(heavy >= 0 && alpha > heavy && zeta > alpha);

        // The covariant pre-interceptor lands in the indirect segment, after the merged
        // direct rows; the grouped final interceptor carries its label.
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.BroadPre))", source);
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.ReportingFinal), new string[] { \"reporting\" })", source);
    }

    [Fact]
    public void Event_BakesEverySubscriberAcrossBothSegments()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public interface IShipment : IEvent;

                public sealed record Shipped : IShipment;

                public sealed class AuditHandler : IEventHandler<Shipped>
                {
                    public ValueTask HandleAsync(Shipped message, ErgosfareContext context) => default;
                }

                public sealed class NotifyHandler : IEventHandler<Shipped>
                {
                    public ValueTask HandleAsync(Shipped message, ErgosfareContext context) => default;
                }

                public sealed class InterfaceHandler : IEventHandler<IShipment>
                {
                    public ValueTask HandleAsync(IShipment message, ErgosfareContext context) => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // Both direct subscribers in ordinal order, and the interface-matched one in the
        // indirect segment — the broadcast's N-handler table.
        var audit = source.IndexOf("FrozenParticipant(typeof(global::TestApp.AuditHandler))", StringComparison.Ordinal);
        var notify = source.IndexOf("FrozenParticipant(typeof(global::TestApp.NotifyHandler))", StringComparison.Ordinal);
        Assert.True(audit >= 0 && notify > audit);
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.InterfaceHandler))", source);
    }

    /// <summary>
    /// The table is the dispatch authority, so it has to carry what the registry used to.
    /// A keyed participant is an ordinary row — whether it runs is settled by what the
    /// container registered, which the consuming catalog knows and the table does not —
    /// while a message's <c>[ExcludeFromPipeline]</c> is resolved at emission: the
    /// covariantly matched interceptor is simply not in the table, its directly registered
    /// sibling still is.
    /// </summary>
    [Fact]
    public void KeyedParticipantsAreRowsAndPipelineExclusionIsBakedIn()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record KeyedPing : ICommand;

                public sealed class KeyedPingHandler : ICommandHandler<KeyedPing>
                {
                    public ValueTask HandleAsync(KeyedPing message, ErgosfareContext context) => default;
                }

                [DiscoveryKey("special")]
                public sealed class KeyedPre : ICommandPreInterceptor<KeyedPing>
                {
                    public ValueTask<KeyedPing> HandleAsync(KeyedPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                // Reached covariantly by CovariantPre, and opted out of exactly that.
                [ExcludeFromPipeline]
                public sealed record ExcludedPing : ICommand;

                public sealed class ExcludedPingHandler : ICommandHandler<ExcludedPing>
                {
                    public ValueTask HandleAsync(ExcludedPing message, ErgosfareContext context) => default;
                }

                public sealed class CovariantPre : ICommandPreInterceptor<ICommand>
                {
                    public ValueTask<ICommand> HandleAsync(ICommand message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                public sealed class ExcludedPingPre : ICommandPreInterceptor<ExcludedPing>
                {
                    public ValueTask<ExcludedPing> HandleAsync(ExcludedPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var blocks = CompositionBlocks(result.GeneratedSource);

        var keyed = Assert.Single(blocks, b => b.Contains("typeof(global::TestApp.KeyedPing)", StringComparison.Ordinal));
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.KeyedPre))", keyed);

        var excluded = Assert.Single(blocks, b => b.Contains("typeof(global::TestApp.ExcludedPing)", StringComparison.Ordinal));
        Assert.DoesNotContain("CovariantPre", excluded);
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.ExcludedPingPre))", excluded);

        // The exclusion is the message's own: its sibling keeps the covariant interceptor.
        var keyedPingBlock = blocks.Single(b => b.Contains("typeof(global::TestApp.KeyedPing)", StringComparison.Ordinal));
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.CovariantPre))", keyedPingBlock);
    }

    /// <summary>
    /// The group-scoped form of the exclusion drops only the covariant interceptors
    /// carrying a named group; the others stay, and so does everything registered against
    /// the message itself.
    /// </summary>
    [Fact]
    public void GroupScopedPipelineExclusion_DropsOnlyTheNamedCovariantGroups()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [ExcludeFromPipeline("audit")]
                public sealed record ScopedPing : ICommand;

                public sealed class ScopedPingHandler : ICommandHandler<ScopedPing>
                {
                    public ValueTask HandleAsync(ScopedPing message, ErgosfareContext context) => default;
                }

                [Group("audit")]
                public sealed class AuditPre : ICommandPreInterceptor<ICommand>
                {
                    public ValueTask<ICommand> HandleAsync(ICommand message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                [Group("tracing")]
                public sealed class TracingPre : ICommandPreInterceptor<ICommand>
                {
                    public ValueTask<ICommand> HandleAsync(ICommand message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var blocks = CompositionBlocks(result.GeneratedSource);
        var scoped = Assert.Single(blocks, b => b.Contains("typeof(global::TestApp.ScopedPing)", StringComparison.Ordinal));

        Assert.DoesNotContain("AuditPre", scoped);
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.TracingPre)", scoped);
    }

    /// <summary>
    /// The ancestor ladder needs somewhere to land: a message hidden from discovery (or a
    /// runtime proxy the compilation never saw) is served by its nearest ancestor's entry,
    /// so abstract message bases get entries even though they are never dispatched
    /// themselves.
    /// </summary>
    [Fact]
    public void AbstractMessageBases_GetTableEntriesForTheLadder()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;

            namespace TestApp
            {
                public abstract class LedgerEntry : ICommand;

                public sealed class LedgerEntryHandler : ICommandHandler<LedgerEntry>
                {
                    public ValueTask HandleAsync(LedgerEntry command, ErgosfareContext context) => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var blocks = CompositionBlocks(result.GeneratedSource);
        var ledger = Assert.Single(blocks, b => b.Contains("typeof(global::TestApp.LedgerEntry)", StringComparison.Ordinal));

        Assert.Contains("FrozenParticipant(typeof(global::TestApp.LedgerEntryHandler))", ledger);
    }

    /// <summary>
    /// A generic message and the participants serving it are baked once, by definition:
    /// the table keys the message unbound, so a single entry serves every instantiation,
    /// and the participants are named unbound too — the dispatch closes them over the
    /// runtime message's arguments.
    /// </summary>
    [Fact]
    public void GenericMessages_BakeOneUnboundEntryServingEveryInstantiation()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;

            namespace TestApp
            {
                public sealed record Wrap<T> : ICommand;

                public sealed class WrapHandler<T> : ICommandHandler<Wrap<T>>
                {
                    public ValueTask HandleAsync(Wrap<T> command, ErgosfareContext context) => default;
                }

                public sealed class WrapPre : ICommandPreInterceptor<Wrap<int>>
                {
                    public ValueTask<Wrap<int>> HandleAsync(Wrap<int> command, ErgosfareContext context)
                        => new(command);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var blocks = CompositionBlocks(result.GeneratedSource);
        var wrap = Assert.Single(blocks, b => b.Contains("typeof(global::TestApp.Wrap<>)", StringComparison.Ordinal));

        Assert.Contains("FrozenParticipant(typeof(global::TestApp.WrapHandler<>))", wrap);

        // An interceptor declared over one instantiation still normalizes to the
        // definition, exactly as the descriptor builders' normalization did — the table
        // has one row set per generic message, not one per instantiation.
        Assert.Contains("FrozenParticipant(typeof(global::TestApp.WrapPre))", wrap);

        // The participant is registered through the batch, never as a message.
        Assert.Contains("participants.Add(typeof(global::TestApp.WrapHandler<>));", result.GeneratedSource);
    }

    /// <summary>Extracts each emitted <c>AddFrozenComposition(...)</c> call as one block.</summary>
    private static List<string> CompositionBlocks(string source)
    {
        var blocks = new List<string>();
        var position = 0;

        while ((position = source.IndexOf("AddFrozenComposition(", position, StringComparison.Ordinal)) >= 0)
        {
            var end = source.IndexOf("));", position, StringComparison.Ordinal);
            blocks.Add(source.Substring(position, end - position));
            position = end;
        }

        return blocks;
    }
}
