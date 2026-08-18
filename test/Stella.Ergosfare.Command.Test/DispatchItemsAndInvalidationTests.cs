using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class ItemsCommand : ICommand { }

public sealed class ItemsCommandHandler : ICommandHandler<ItemsCommand>
{
    public ValueTask HandleAsync(ItemsCommand command, ErgosfareContext context)
    {
        context.Set("writtenByHandler", "yes");
        return ValueTask.CompletedTask;
    }
}

public sealed class ItemsResultCommand : ICommand<int> { }

public sealed class ItemsResultCommandHandler : ICommandHandler<ItemsResultCommand, int>
{
    public ValueTask<int> HandleAsync(ItemsResultCommand command, ErgosfareContext context)
        => ValueTask.FromResult(42);
}

/// <summary>
/// Covers the dispatch fast path's interaction with caller-supplied settings items —
/// the pooled context adopts the caller's dictionary for the dispatch and detaches it
/// untouched on return. Fixtures are top-level and discoverable, so the generator bakes
/// their pipelines and the dispatches run through compiled plans.
/// </summary>
public class DispatchItemsAndInvalidationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Send_ShouldNotWipeCallerSettingsItems_AndShouldExposeHandlerWrites()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ItemsCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var settings = new ErgosfareContext();
        settings.Items["keep"] = "me";

        await mediator.SendAsync(new ItemsCommand(), settings);

        Assert.Equal("me", settings.Items["keep"]);
        Assert.Equal("yes", settings.Items["writtenByHandler"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Send_ShouldNotLeakOneDispatchesItems_IntoTheNext()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ItemsCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var first = new ErgosfareContext();
        first.Items["secret"] = "data";
        await mediator.SendAsync(new ItemsCommand(), first);

        var second = new ErgosfareContext();
        await mediator.SendAsync(new ItemsCommand(), second);

        // The second dispatch's pooled context must not surface the first caller's items,
        // and writing during the second dispatch must not reach the first caller.
        Assert.False(second.Items.ContainsKey("secret"));
        Assert.Equal("data", first.Items["secret"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Send_ResultCommand_ShouldFlowThroughTheResultFastPath()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ItemsResultCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Repeated sends exercise the cached result-executor lookup.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(42, await mediator.SendAsync(new ItemsResultCommand()));
        }
    }
}
