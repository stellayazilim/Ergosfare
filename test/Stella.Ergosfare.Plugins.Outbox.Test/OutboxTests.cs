using System.Collections.Concurrent;
using Stella.Ergosfare.Plugins.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;

namespace OutboxTests;

[OutboxMessage("product-created/v1")]
public sealed partial record ProductCreated(int Id);
public sealed record CreateProduct(int Id) : ICommand;
public sealed class ScopeIdentity : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool Disposed { get; private set; }
    public void Dispose() => Disposed = true;
}
public sealed class Observations
{
    public ConcurrentQueue<(int Product, ScopeIdentity Scope)> Delivered { get; } = new();
    public int FailuresRemaining;
    public int Active;
    public int Peak;
    public int NormalDeliveries;
    public TaskCompletionSource? Release;
    public TaskCompletionSource TwoStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
public sealed class NormalProductCreatedHandler(Observations observations) : IEventHandler<ProductCreated>
{
    public ValueTask HandleAsync(ProductCreated message, ErgosfareContext context)
    {
        Interlocked.Increment(ref observations.NormalDeliveries);
        return ValueTask.CompletedTask;
    }
}
public sealed class CreateProductHandler : ICommandHandler<CreateProduct>
{
    public ValueTask HandleAsync(CreateProduct message, ErgosfareContext context)
        => context.EnqueueOutboxAsync(new ProductCreated(message.Id));
}
public sealed class ProductCreatedHandler(Observations observations, ScopeIdentity scope)
    : IOutboxHandler<ProductCreated>
{
    public async ValueTask HandleAsync(OutboxEvent<ProductCreated> delivery, ErgosfareContext context)
    {
        var message = delivery.Message;
        if (Interlocked.Decrement(ref observations.FailuresRemaining) >= 0)
            throw new InvalidOperationException("Transient delivery failure");
        var active = Interlocked.Increment(ref observations.Active);
        int previous;
        do { previous = observations.Peak; }
        while (active > previous && Interlocked.CompareExchange(ref observations.Peak, active, previous) != previous);
        if (active >= 2) observations.TwoStarted.TrySetResult();
        try
        {
            if (observations.Release is { } release) await release.Task.WaitAsync(context.CancellationToken);
            observations.Delivered.Enqueue((message.Id, scope));
        }
        finally { Interlocked.Decrement(ref observations.Active); }
    }
}

[Trait("Category", "Unit")]
public sealed class OutboxTests
{
    private static ServiceProvider Build(Observations observations, IOutboxStore? store = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(observations);
        services.AddScoped<ScopeIdentity>();
        services.AddErgosfare(options =>
        {
            options.AddOutboxPlugin(outbox =>
            {
                if (store is null) outbox.UseInMemory();
                else outbox.UseStore(services => services.AddSingleton(store));
                outbox.RetryDelay = TimeSpan.Zero;
                outbox.MaxConcurrency = 2;
                outbox.PollInterval = TimeSpan.FromMilliseconds(10);
                outbox.LeaseDuration = TimeSpan.FromMilliseconds(150);
            });
            options.AddCommandModule(commands => commands.AddGenerated());
            options.AddEventModule(events => events.AddGenerated());
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task ContextEnqueue_UsesGeneratedCommand_ThenIndependentEventScope()
    {
        var observations = new Observations();
        await using var provider = Build(observations);
        Guid requestScope;
        await using (var scope = provider.CreateAsyncScope())
        {
            requestScope = scope.ServiceProvider.GetRequiredService<ScopeIdentity>().Id;
            await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CreateProduct(42));
        }
        Assert.Empty(observations.Delivered);
        var worker = Assert.IsType<OutboxBackgroundService>(provider.GetServices<IHostedService>().Single());
        Assert.True(await worker.ProcessOneAsync());
        var delivered = Assert.Single(observations.Delivered);
        Assert.Equal(42, delivered.Product);
        Assert.NotEqual(requestScope, delivered.Scope.Id);
        Assert.True(delivered.Scope.Disposed);
        Assert.False(await worker.ProcessOneAsync());
        Assert.Equal(0, observations.NormalDeliveries);
        await using var normalScope = provider.CreateAsyncScope();
        await normalScope.ServiceProvider.GetRequiredService<IEventMediator>().PublishAsync(new ProductCreated(99));
        Assert.Equal(1, observations.NormalDeliveries);
        Assert.Single(observations.Delivered);
    }

    [Fact]
    public async Task FailedDispatch_IsRetried_ThenAcknowledged()
    {
        var observations = new Observations { FailuresRemaining = 1 };
        await using var provider = Build(observations);
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CreateProduct(1));
        var worker = Assert.IsType<OutboxBackgroundService>(provider.GetServices<IHostedService>().Single());
        Assert.True(await worker.ProcessOneAsync());
        Assert.Empty(observations.Delivered);
        Assert.True(await worker.ProcessOneAsync());
        Assert.Single(observations.Delivered);
        Assert.False(await worker.ProcessOneAsync());
    }

    [Fact]
    public async Task ExpiredOwner_CannotAcknowledgeReclaimedMessage()
    {
        var clock = new TestClock();
        var store = new InMemoryOutboxStore(clock);
        await store.AppendAsync(new(Guid.NewGuid(), "test/v1", [1]), default);
        var first = (await store.ClaimAsync(TimeSpan.FromSeconds(1), default))!;
        Assert.Null(await store.ClaimAsync(TimeSpan.FromSeconds(1), default));
        clock.Now += TimeSpan.FromSeconds(2);
        var second = (await store.ClaimAsync(TimeSpan.FromSeconds(1), default))!;
        await store.CompleteAsync(first, default);
        Assert.True(await store.RenewAsync(second, TimeSpan.FromSeconds(1), default));
        await store.CompleteAsync(second, default);
        Assert.Null(await store.ClaimAsync(TimeSpan.FromSeconds(1), default));
    }

    [Fact]
    public async Task MissingModules_FailsAtWorkerStartRegardlessOfConfigurationOrder()
    {
        var services = new ServiceCollection();
        services.AddErgosfare(o => o.AddOutboxPlugin(x => x.UseInMemory()));
        await using var provider = services.BuildServiceProvider();
        var worker = provider.GetServices<IHostedService>().Single();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => worker.StartAsync(default));
        Assert.Contains("AddCommandModule", error.Message);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Keep lease time deterministic while observing the worker's real renewal timer.
    // Delivery is observable before acknowledgment; shutdown must wait for the latter.
    private sealed class ObservedStore : IOutboxStore
    {
        private readonly InMemoryOutboxStore inner = new(new TestClock());
        private readonly ConcurrentDictionary<Guid, int> renewals = new();
        private int completed;
        public TaskCompletionSource Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask AppendAsync(OutboxMessage message, CancellationToken ct) => inner.AppendAsync(message, ct);
        public ValueTask<OutboxLease?> ClaimAsync(TimeSpan duration, CancellationToken ct) => inner.ClaimAsync(duration, ct);
        public async ValueTask<bool> RenewAsync(OutboxLease lease, TimeSpan duration, CancellationToken ct)
        {
            var renewed = await inner.RenewAsync(lease, duration, ct);
            if (renewed)
            {
                renewals.AddOrUpdate(lease.Token, 1, (_, count) => count + 1);
                if (renewals.Count(pair => pair.Value >= 2) >= 2) Renewed.TrySetResult();
            }
            return renewed;
        }
        public async ValueTask CompleteAsync(OutboxLease lease, CancellationToken ct)
        {
            await inner.CompleteAsync(lease, ct);
            if (Interlocked.Increment(ref completed) == 6) Completed.TrySetResult();
        }
        public ValueTask FailAsync(OutboxLease lease, string error, TimeSpan delay, bool deadLetter, CancellationToken ct)
            => inner.FailAsync(lease, error, delay, deadLetter, ct);
    }

    [Fact]
    public async Task HostedPolling_BoundsConcurrency_RenewsLeases_AndDisposesScopes()
    {
        var observations = new Observations
        {
            Release = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var store = new ObservedStore();
        await using var provider = Build(observations, store);
        await using (var scope = provider.CreateAsyncScope())
            for (var i = 0; i < 6; i++)
                await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CreateProduct(i));
        var worker = Assert.IsType<OutboxBackgroundService>(provider.GetServices<IHostedService>().Single());
        await worker.StartAsync(default);
        try
        {
            await observations.TwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await store.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(2, observations.Active);
            Assert.Equal(2, observations.Peak);
            observations.Release.SetResult();
            await store.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { await worker.StopAsync(default); }
        Assert.Equal(6, observations.Delivered.Count);
        Assert.Equal(6, observations.Delivered.Select(x => x.Product).Distinct().Count());
        Assert.All(observations.Delivered, item => Assert.True(item.Scope.Disposed));
        Assert.False(await worker.ProcessOneAsync());
    }

    [Fact]
    public async Task Shutdown_ReleasesFailedWork_ForAnotherWorker()
    {
        var observations = new Observations
        {
            Release = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        await using var provider = Build(observations);
        await using (var scope = provider.CreateAsyncScope())
            for (var i = 0; i < 2; i++)
                await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CreateProduct(i));
        var worker = Assert.IsType<OutboxBackgroundService>(provider.GetServices<IHostedService>().Single());
        await worker.StartAsync(default);
        try { await observations.TwoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { await worker.StopAsync(default); }
        Assert.Equal(0, observations.Active);
        Assert.Empty(observations.Delivered);
        observations.Release.SetResult();
        Assert.True(await worker.ProcessOneAsync());
        Assert.True(await worker.ProcessOneAsync());
        Assert.Equal(2, observations.Delivered.Count);
    }

    [Fact]
    public async Task ExhaustedAttempts_StopPollingTheRecord()
    {
        var observations = new Observations { FailuresRemaining = 100 };
        await using var provider = Build(observations);
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CreateProduct(1));
        var worker = Assert.IsType<OutboxBackgroundService>(provider.GetServices<IHostedService>().Single());
        for (var i = 0; i < 5; i++) Assert.True(await worker.ProcessOneAsync());
        Assert.False(await worker.ProcessOneAsync());
        Assert.Empty(observations.Delivered);
    }

    [Fact]
    public void StoreSelection_IsRequired()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddErgosfare(o => o.AddOutboxPlugin(_ => { })));
    }
}
