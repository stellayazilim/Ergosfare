using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The grouped executor lookup's last-used slot: alternating group sets on one message
/// type must always dispatch the requested group's executor (the composite store stays
/// authoritative on a slot miss), for the void and the result shape alike. Helper types
/// are excluded from discovery so assembly scans cannot alter these pipelines.
/// </summary>
public class GroupedExecutorSlotTests
{
    [ExcludeFromDiscovery]
    public sealed class RoutedCommand : ICommand { }

    [ExcludeFromDiscovery]
    [Group("east")]
    public sealed class EastHandler : ICommandHandler<RoutedCommand>
    {
        public ValueTask HandleAsync(RoutedCommand command, ErgosfareContext context)
        {
            context.Set("ran", "east");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    [Group("west")]
    public sealed class WestHandler : ICommandHandler<RoutedCommand>
    {
        public ValueTask HandleAsync(RoutedCommand command, ErgosfareContext context)
        {
            context.Set("ran", "west");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AlternatingGroupSets_DispatchTheRequestedGroupsExecutor()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<EastHandler>();
                c.Register<WestHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        Assert.Equal("east", await Dispatch(mediator, "east"));
        Assert.Equal("west", await Dispatch(mediator, "west"));
        Assert.Equal("east", await Dispatch(mediator, "east"));

        static async Task<string> Dispatch(ICommandMediator mediator, params string[] groups)
        {
            var items = new ErgosfareContext();
            await mediator.SendAsync(new RoutedCommand(), items, groups);
            return Assert.IsType<string>(items.Items["ran"]);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class RoutedEcho : ICommand<string> { }

    [ExcludeFromDiscovery]
    [Group("east")]
    public sealed class EastEchoHandler : ICommandHandler<RoutedEcho, string>
    {
        public ValueTask<string> HandleAsync(RoutedEcho command, ErgosfareContext context)
            => ValueTask.FromResult("east");
    }

    [ExcludeFromDiscovery]
    [Group("west")]
    public sealed class WestEchoHandler : ICommandHandler<RoutedEcho, string>
    {
        public ValueTask<string> HandleAsync(RoutedEcho command, ErgosfareContext context)
            => ValueTask.FromResult("west");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AlternatingGroupSets_DispatchTheRequestedGroupsResultExecutor()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<EastEchoHandler>();
                c.Register<WestEchoHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        Assert.Equal("east", await Dispatch(mediator, "east"));
        Assert.Equal("west", await Dispatch(mediator, "west"));
        Assert.Equal("east", await Dispatch(mediator, "east"));

        static async Task<string> Dispatch(ICommandMediator mediator, params string[] groups)
        {
            return await mediator.SendAsync(new RoutedEcho(), new ErgosfareContext(null), groups);
        }
    }
}
