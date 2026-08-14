using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Pins DI lifetimes for a message whose every handler lives in a named group. The
/// group-less shape of such a message is empty, and an empty shape is vacuously
/// "all singleton" — a per-type memoization cache once let that vacuous verdict leak into
/// the grouped shape, silently promoting its transient handlers to de-facto singletons.
/// The verdict is a property of the shape, never of the type; these tests hold that line.
/// </summary>
public class GroupOnlyLifetimeTests
{
    public sealed class GroupOnlyEvent : IEvent { }

    [Group("lifetime")]
    public sealed class GroupOnlyHandler : IEventHandler<GroupOnlyEvent>
    {
        public ValueTask HandleAsync(GroupOnlyEvent @event, ErgosfareContext context)
        {
            var instances = (HashSet<int>)context.Items["instances"]!;
            instances.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this));
            return ValueTask.CompletedTask;
        }
    }

    private static readonly GroupSet LifetimeGroup = GroupSet.Of("lifetime");

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GroupedPublish_AfterGrouplessTouch_KeepsTransientHandlerLifetime()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<GroupOnlyHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();

        var instances = new HashSet<int>();
        var context = new ErgosfareContext();
        context.Items["instances"] = instances;

        // The group-less publish first: it settles the type's empty default shape — the
        // exact order that once poisoned the per-type singleton verdict.
        await mediator.PublishAsync(new GroupOnlyEvent(), context);

        await mediator.PublishAsync(new GroupOnlyEvent(), context, LifetimeGroup);
        await mediator.PublishAsync(new GroupOnlyEvent(), context, LifetimeGroup);
        await mediator.PublishAsync(new GroupOnlyEvent(), context, LifetimeGroup);

        // A transient handler is a fresh instance per delivery; one shared instance means
        // the pipeline was silently memoized.
        Assert.Equal(3, instances.Count);
    }
}
