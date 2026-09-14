using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class SlotRoutedCommand : ICommand { }

[Group("east")]
public sealed class SlotEastHandler : ICommandHandler<SlotRoutedCommand>
{
    public ValueTask HandleAsync(SlotRoutedCommand command, ErgosfareContext context)
    {
        context.Set("ran", "east");
        return ValueTask.CompletedTask;
    }
}

[Group("west")]
public sealed class SlotWestHandler : ICommandHandler<SlotRoutedCommand>
{
    public ValueTask HandleAsync(SlotRoutedCommand command, ErgosfareContext context)
    {
        context.Set("ran", "west");
        return ValueTask.CompletedTask;
    }
}

public sealed class SlotRoutedEcho : ICommand<string> { }

[Group("east")]
public sealed class SlotEastEchoHandler : ICommandHandler<SlotRoutedEcho, string>
{
    public ValueTask<string> HandleAsync(SlotRoutedEcho command, ErgosfareContext context)
        => ValueTask.FromResult("east");
}

[Group("west")]
public sealed class SlotWestEchoHandler : ICommandHandler<SlotRoutedEcho, string>
{
    public ValueTask<string> HandleAsync(SlotRoutedEcho command, ErgosfareContext context)
        => ValueTask.FromResult("west");
}

/// <summary>
/// The grouped executor lookup's last-used slot: alternating group sets on one message
/// type must always dispatch the requested group's executor (the composite store stays
/// authoritative on a slot miss), for the void and the result shape alike. Fixtures are
/// top-level and discoverable, and every set is a literal at its dispatch site, so the
/// generator bakes one plan per set — a set it cannot read would leave a two-handler
/// message with no plan to run.
/// </summary>
public class GroupedExecutorSlotTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AlternatingGroupSets_DispatchTheRequestedGroupsExecutor()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<SlotEastHandler>();
                c.Register<SlotWestHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var first = new ErgosfareContext();
        await mediator.SendAsync(new SlotRoutedCommand(), first, ["east"]);
        Assert.Equal("east", first.Items["ran"]);

        var second = new ErgosfareContext();
        await mediator.SendAsync(new SlotRoutedCommand(), second, ["west"]);
        Assert.Equal("west", second.Items["ran"]);

        var third = new ErgosfareContext();
        await mediator.SendAsync(new SlotRoutedCommand(), third, ["east"]);
        Assert.Equal("east", third.Items["ran"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AlternatingGroupSets_DispatchTheRequestedGroupsResultExecutor()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<SlotEastEchoHandler>();
                c.Register<SlotWestEchoHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        Assert.Equal("east", await mediator.SendAsync(new SlotRoutedEcho(), new ErgosfareContext(), ["east"]));
        Assert.Equal("west", await mediator.SendAsync(new SlotRoutedEcho(), new ErgosfareContext(), ["west"]));
        Assert.Equal("east", await mediator.SendAsync(new SlotRoutedEcho(), new ErgosfareContext(), ["east"]));
    }
}
