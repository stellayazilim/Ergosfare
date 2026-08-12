using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The <see cref="GroupSet"/> facade overloads on the command mediator: filtering
/// correctness for the void and result shapes, the canonical slot fast path under
/// repetition and alternation, the empty set routing to the default pipeline, and the
/// legacy settings lane accepting a <see cref="GroupSet"/> as its group sequence.
/// Helper types are excluded from discovery so assembly scans cannot alter these
/// pipelines; handlers record into a static slot, which is safe because the types are
/// private to this class and tests within a class run sequentially.
/// </summary>
public class GroupSetDispatchTests
{
    private static readonly GroupSet East = GroupSet.Of("gs.east");
    private static readonly GroupSet West = GroupSet.Of("gs.west");

    private static string? _lastRan;

    [ExcludeFromDiscovery]
    public sealed class SlottedCommand : ICommand { }

    [ExcludeFromDiscovery]
    [Group("gs.east")]
    public sealed class EastSlottedHandler : ICommandHandler<SlottedCommand>
    {
        public ValueTask HandleAsync(SlottedCommand command, ErgosfareContext context)
        {
            _lastRan = "east";
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    [Group("gs.west")]
    public sealed class WestSlottedHandler : ICommandHandler<SlottedCommand>
    {
        public ValueTask HandleAsync(SlottedCommand command, ErgosfareContext context)
        {
            _lastRan = "west";
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class DefaultCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class DefaultCommandHandler : ICommandHandler<DefaultCommand>
    {
        public ValueTask HandleAsync(DefaultCommand command, ErgosfareContext context)
        {
            _lastRan = "default";
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class SlottedEcho : ICommand<string> { }

    [ExcludeFromDiscovery]
    [Group("gs.east")]
    public sealed class EastEchoSlottedHandler : ICommandHandler<SlottedEcho, string>
    {
        public ValueTask<string> HandleAsync(SlottedEcho command, ErgosfareContext context)
            => ValueTask.FromResult("east");
    }

    [ExcludeFromDiscovery]
    [Group("gs.west")]
    public sealed class WestEchoSlottedHandler : ICommandHandler<SlottedEcho, string>
    {
        public ValueTask<string> HandleAsync(SlottedEcho command, ErgosfareContext context)
            => ValueTask.FromResult("west");
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<EastSlottedHandler>();
                c.Register<WestSlottedHandler>();
                c.Register<DefaultCommandHandler>();
                c.Register<EastEchoSlottedHandler>();
                c.Register<WestEchoSlottedHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task VoidOverload_FiltersAndSurvivesRepetitionAndAlternation()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Repetition exercises the canonical reference fast path; alternation exercises
        // the slot refresh with the authoritative store underneath.
        _lastRan = null;
        await mediator.SendAsync(new SlottedCommand(), East);
        Assert.Equal("east", _lastRan);

        await mediator.SendAsync(new SlottedCommand(), East);
        Assert.Equal("east", _lastRan);

        await mediator.SendAsync(new SlottedCommand(), West);
        Assert.Equal("west", _lastRan);

        await mediator.SendAsync(new SlottedCommand(), East);
        Assert.Equal("east", _lastRan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ResultOverload_BindsTheResultShape_AndFilters()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Overload-resolution fact under test as much as behavior: a result command with
        // a GroupSet must bind the result overload, never the void one.
        Assert.Equal("east", await mediator.SendAsync(new SlottedEcho(), East));
        Assert.Equal("west", await mediator.SendAsync(new SlottedEcho(), West));
        Assert.Equal("east", await mediator.SendAsync(new SlottedEcho(), East));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task EmptySet_DispatchesTheDefaultPipeline()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        _lastRan = null;
        await mediator.SendAsync(new DefaultCommand(), GroupSet.Empty);

        // Empty filter == no filter: the default-group pipeline runs.
        Assert.Equal("default", _lastRan);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task SettingsLane_AcceptsAGroupSetAsItsGroupSequence()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The legacy lane: a GroupSet assigned to Filters.Groups behaves as the same
        // filter, and the caches recognize the canonical instance there too.
        _lastRan = null;
        var settings = new CommandMediationSettings { Filters = { Groups = West } };
        await mediator.SendAsync(new SlottedCommand(), settings);

        Assert.Equal("west", _lastRan);
    }
}
