using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Verifies that handler resolution honors registered DI lifetimes by default
/// (scoped dependencies are isolated per scope) and that
/// <c>ForceMemoizedHandlers()</c> restores the process-wide memoized behavior.
/// </summary>
public class HandlerLifetimeResolutionTests
{
    public sealed class ScopedProbe : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    public sealed class ScopedDepCommand : ICommand
    {
        public Guid ObservedDepId;
        public ScopedProbe? ObservedProbe;
    }

    public sealed class ScopedDepCommandHandler(ScopedProbe probe) : ICommandHandler<ScopedDepCommand>
    {
        public ValueTask HandleAsync(ScopedDepCommand message, IExecutionContext context)
        {
            message.ObservedDepId = probe.Id;
            message.ObservedProbe = probe;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class SingletonCommand : ICommand
    {
        public Guid ObservedHandlerId;
    }

    public sealed class SingletonCommandHandler : ICommandHandler<SingletonCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public ValueTask HandleAsync(SingletonCommand message, IExecutionContext context)
        {
            message.ObservedHandlerId = _id;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class ForcedCommand : ICommand
    {
        public Guid ObservedDepId;
    }

    public sealed class ForcedCommandHandler(ScopedProbe probe) : ICommandHandler<ForcedCommand>
    {
        public ValueTask HandleAsync(ForcedCommand message, IExecutionContext context)
        {
            message.ObservedDepId = probe.Id;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ScopedDependency_IsResolvedPerScope_ByDefault()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
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
        services.AddSingleton<SingletonCommandHandler>();
        services.AddErgosfare(o => o.AddCommandModule(m => m.Register<SingletonCommandHandler>()));
        await using var provider = services.BuildServiceProvider();

        var firstCommand = new SingletonCommand();
        var secondCommand = new SingletonCommand();

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
    public async Task ForceMemoizedHandlers_ReusesFirstInstanceAcrossScopes()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopedProbe>();
        services.AddErgosfare(o =>
        {
            o.ForceMemoizedHandlers();
            o.AddCommandModule(m => m.Register<ForcedCommandHandler>());
        });
        await using var provider = services.BuildServiceProvider();

        var firstCommand = new ForcedCommand();
        var secondCommand = new ForcedCommand();

        // act
        using (var scope1 = provider.CreateScope())
        {
            await scope1.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(firstCommand);
        }

        using (var scope2 = provider.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(secondCommand);
        }

        // assert: pre-v1.2 behavior — one memoized instance for everyone
        Assert.Equal(firstCommand.ObservedDepId, secondCommand.ObservedDepId);
    }
}
