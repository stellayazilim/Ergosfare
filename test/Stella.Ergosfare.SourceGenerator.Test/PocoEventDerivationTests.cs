namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// A domain event declared without an Ergosfare reference. The publish lane is written over
/// <c>notnull</c> from end to end — <c>IEventHandler&lt;TEvent&gt;</c>,
/// <c>PublishAsync&lt;TEvent&gt;</c>, <c>FrozenBroadcastDispatch&lt;TEvent&gt;</c> — so nothing
/// about carrying a value needs the marker. What needed it was being <em>seen</em>: a plain
/// type has no base list, so its declaration is never visited, and nothing else claimed it.
/// </summary>
/// <remarks>
/// The subscriber's signature is what settles it. <c>IEventHandler&lt;OrderPlaced&gt;</c> does
/// not point at a message the generator then goes looking for — it is what makes
/// <c>OrderPlaced</c> one, which is why the message's own declaration never has to be found.
/// </remarks>
public class PocoEventDerivationTests
{
    private const string Source = """
        using Stella.Ergosfare.Events.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using System.Threading.Tasks;

        namespace TestApp
        {
            // No marker, no base list — the shape a domain layer wants.
            public sealed record OrderPlaced(int Id);

            public sealed class OrderPlacedHandler : IEventHandler<OrderPlaced>
            {
                public ValueTask HandleAsync(OrderPlaced @event, ErgosfareContext context) => default;
            }
        }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void APlainTypeASubscriberNames_BecomesAnEventMessage()
    {
        var result = GeneratorTestHost.Run(Source);

        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // The subscriber was always collected — its contract carries the marker. What was
        // missing is everything the message needs to have a pipeline at all.
        Assert.Contains("typeof(global::TestApp.OrderPlacedHandler)", source);
        Assert.Contains("global::TestApp.OrderPlaced", source);
        Assert.Contains("compositions.Select(typeof(global::TestApp.OrderPlaced))", source);

        // No message root: the type implements no marker, and a publish never needs one.
        Assert.DoesNotContain("AddMessage<global::TestApp.OrderPlaced>", source);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AMarkedEvent_IsNotDerivedTwice()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Events.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record OrderShipped(int Id) : IEvent;

                public sealed class OrderShippedHandler : IEventHandler<OrderShipped>
                {
                    public ValueTask HandleAsync(OrderShipped @event, ErgosfareContext context) => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);

        // A message carrying its own marker is registrable on its own terms; deriving it
        // again would only produce a duplicate for the pipeline to drop.
        var source = result.GeneratedSource;
        var occurrences = source.Split("compositions.Select(typeof(global::TestApp.OrderShipped))").Length - 1;

        Assert.Equal(1, occurrences);

        // Marked, so it is a message on its own terms and keeps its root.
        Assert.Contains("AddMessage<global::TestApp.OrderShipped>", source);
    }
}
