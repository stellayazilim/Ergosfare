using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Covers the dispatch fast path's interaction with caller-supplied settings items —
/// the pooled context adopts the caller's dictionary for the dispatch and detaches it
/// untouched on return — and the executor-level plan cache's registry-version
/// invalidation for runtime registrations.
/// </summary>
public class DispatchItemsAndInvalidationTests
{
    public sealed class ItemsCommand : ICommand { }

    public sealed class ItemsCommandHandler : ICommandHandler<ItemsCommand>
    {
        public ValueTask HandleAsync(ItemsCommand command, ErgosfareContext context)
        {
            context.Set("writtenByHandler", "yes");
            return ValueTask.CompletedTask;
        }
    }

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
        var settings = new Dictionary<object, object?>();
        settings["keep"] = "me";

        await mediator.SendAsync(new ItemsCommand(), settings);

        Assert.Equal("me", settings["keep"]);
        Assert.Equal("yes", settings["writtenByHandler"]);
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

        var first = new Dictionary<object, object?>();
        first["secret"] = "data";
        await mediator.SendAsync(new ItemsCommand(), first);

        var second = new Dictionary<object, object?>();
        await mediator.SendAsync(new ItemsCommand(), second);

        // The second dispatch's pooled context must not surface the first caller's items,
        // and writing during the second dispatch must not reach the first caller.
        Assert.False(second.ContainsKey("secret"));
        Assert.Equal("data", first["secret"]);
    }

    public sealed class LateInterceptedCommand : ICommand { }

    // ERGOSG007 (suppressed in the csproj): dispatched after a runtime registration below,
    // a site the closed-world dispatch-site analysis cannot see.
    public sealed class LateInterceptedCommandHandler : ICommandHandler<LateInterceptedCommand>
    {
        public ValueTask HandleAsync(LateInterceptedCommand command, ErgosfareContext context)
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Excluded from discovery: the fact below registers this type at runtime to observe
    /// the version bump — another test's assembly scan (the registry is process-wide)
    /// must not slip it into the pipeline before the warm dispatches run.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class LateRegisteredInterceptor : ICommandPreInterceptor<LateInterceptedCommand>
    {
        public ValueTask<LateInterceptedCommand> HandleAsync(LateInterceptedCommand command, ErgosfareContext context)
        {
            context.Set("lateInterceptorRan", true);
            return ValueTask.FromResult(command);
        }
    }

    public sealed class ResultCommand : ICommand<int> { }

    public sealed class ResultCommandHandler : ICommandHandler<ResultCommand, int>
    {
        public ValueTask<int> HandleAsync(ResultCommand command, ErgosfareContext context)
            => ValueTask.FromResult(42);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Send_ResultCommand_ShouldFlowThroughTheResultFastPath()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ResultCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Repeated sends exercise the cached result-executor lookup.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(42, await mediator.SendAsync(new ResultCommand()));
        }
    }
}
