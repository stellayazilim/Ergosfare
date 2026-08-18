using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class LifetimeScopedProbe : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool Disposed;
    public void Dispose() => Disposed = true;
}

public sealed class ScopedDepCommand : ICommand
{
    public Guid ObservedDepId;
    public LifetimeScopedProbe? ObservedProbe;
}

public sealed class ScopedDepCommandHandler(LifetimeScopedProbe probe) : ICommandHandler<ScopedDepCommand>
{
    public ValueTask HandleAsync(ScopedDepCommand message, ErgosfareContext context)
    {
        message.ObservedDepId = probe.Id;
        message.ObservedProbe = probe;
        return ValueTask.CompletedTask;
    }
}

public sealed class LifetimeSingletonCommand : ICommand
{
    public Guid ObservedHandlerId;
}

public sealed class LifetimeSingletonCommandHandler : ICommandHandler<LifetimeSingletonCommand>
{
    private readonly Guid _id = Guid.NewGuid();

    public ValueTask HandleAsync(LifetimeSingletonCommand message, ErgosfareContext context)
    {
        message.ObservedHandlerId = _id;
        return ValueTask.CompletedTask;
    }
}

public sealed class ForcedCommand : ICommand
{
    public Guid ObservedDepId;
}

public sealed class ForcedCommandHandler(LifetimeScopedProbe probe) : ICommandHandler<ForcedCommand>
{
    public ValueTask HandleAsync(ForcedCommand message, ErgosfareContext context)
    {
        message.ObservedDepId = probe.Id;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Verifies that handler resolution honors registered DI lifetimes by default
/// (scoped dependencies are isolated per scope) and that <c>ForceMemoizedHandlers()</c> —
/// a contract no compiled plan can keep — now fails the dispatch loudly.
/// </summary>
public class HandlerLifetimeResolutionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScopedDependency_IsResolvedPerScope_ByDefault()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddScoped<LifetimeScopedProbe>();
        services.AddErgosfare(o => o.AddCommandModule(m => m.Register<ScopedDepCommandHandler>()));
        await using var provider = services.BuildServiceProvider();

        var firstCommand = new ScopedDepCommand();
        var secondCommand = new ScopedDepCommand();

        // act
        using (var scope1 = provider.CreateScope())
        {
            await scope1.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(firstCommand);
        }

        using (var scope2 = provider.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(secondCommand);

            // scope2's dependency is still alive within its own scope...
            Assert.False(secondCommand.ObservedProbe!.Disposed);
            // ...while scope1's dependency was disposed together with scope1
            Assert.True(firstCommand.ObservedProbe!.Disposed);
        }

        // assert: each scope observed its own dependency instance
        Assert.NotEqual(firstCommand.ObservedDepId, secondCommand.ObservedDepId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SingletonRegisteredHandler_IsMemoizedAcrossScopes()
    {
        // arrange: an explicit singleton registration before AddErgosfare wins over
        // the module's TryAddTransient and keeps the memoized fast path
        var services = new ServiceCollection();
        services.AddSingleton<LifetimeSingletonCommandHandler>();
        services.AddErgosfare(o => o.AddCommandModule(m => m.Register<LifetimeSingletonCommandHandler>()));
        await using var provider = services.BuildServiceProvider();

        var firstCommand = new LifetimeSingletonCommand();
        var secondCommand = new LifetimeSingletonCommand();

        // act
        using (var scope1 = provider.CreateScope())
        {
            await scope1.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(firstCommand);
        }

        using (var scope2 = provider.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(secondCommand);
        }

        // assert
        Assert.Equal(firstCommand.ObservedHandlerId, secondCommand.ObservedHandlerId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ForceMemoizedHandlers_FailsTheDispatch()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddScoped<LifetimeScopedProbe>();
        services.AddErgosfare(o =>
        {
            o.ForceMemoizedHandlers();
            o.AddCommandModule(m => m.Register<ForcedCommandHandler>());
        });
        await using var provider = services.BuildServiceProvider();

        // act + assert: a memoized pipeline caches instances inside its references, and a
        // compiled plan resolves fresh ones — the two contracts cannot both hold, so the
        // construct is unplanned until the generator learns it and every dispatch fails.
        using var scope = provider.CreateScope();
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new ForcedCommand()));

        Assert.Equal(UnplannedDispatchReason.MemoizedInstances, thrown.Reason);
        Assert.Equal(typeof(ForcedCommand), thrown.MessageType);
    }
}
