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
/// Verifies scoped dependency isolation and transient handler construction.
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
    public async Task ParameterlessHandler_IsConstructedPerDispatch()
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
        Assert.NotEqual(firstCommand.ObservedHandlerId, secondCommand.ObservedHandlerId);
    }

}
