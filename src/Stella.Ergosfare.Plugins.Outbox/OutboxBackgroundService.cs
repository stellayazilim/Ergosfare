using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Plugins.Outbox;

/// <summary>Bounded polling. Each occupied slot owns its scope through acknowledgment and cleanup.</summary>
public sealed class OutboxBackgroundService(
    IServiceScopeFactory scopes, OutboxOptions options, ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            if (scope.ServiceProvider.GetService<ICommandMediator>() is null
                || scope.ServiceProvider.GetService<IEventMediator>() is null)
                throw new InvalidOperationException("AddOutboxPlugin requires both AddCommandModule and AddEventModule.");
            _ = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        }
        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.WhenAll(Enumerable.Range(0, options.MaxConcurrency).Select(_ => PollAsync(stoppingToken)));

    private async Task PollAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessOneAsync(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox polling failed; outstanding leases will expire.");
            }
            try { await Task.Delay(options.PollInterval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    /// <summary>Processes at most one record. Also usable by a manually scheduled host.</summary>
    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var lease = await store.ClaimAsync(options.LeaseDuration, cancellationToken);
        if (lease is null) return false;

        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var renewalStop = new CancellationTokenSource();
        var renewal = RenewAsync(lease, execution, renewalStop.Token);
        Exception? failure = null;
        try
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IEventMediator>();
            await options.Find(lease.Message.Contract).DispatchAsync(lease.Message.Payload, mediator, execution.Token);
        }
        catch (Exception exception) { failure = exception; }
        finally
        {
            await renewalStop.CancelAsync();
            await renewal;
        }

        // A bounded cleanup token is independent of host cancellation. A failed write
        // leaves the lease recoverable; this does not promise exactly-once side effects.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        if (failure is null && !execution.IsCancellationRequested)
            await store.CompleteAsync(lease, cleanup.Token);
        else
        {
            logger.LogWarning(failure, "Outbox delivery {MessageId} failed at attempt {Attempt}.", lease.Message.Id, lease.Attempt);
            await store.FailAsync(lease, failure?.Message ?? "Delivery cancelled or lease lost.", options.RetryDelay,
                !cancellationToken.IsCancellationRequested && lease.Attempt >= options.MaxAttempts, cleanup.Token);
        }
        return true;
    }

    private async Task RenewAsync(OutboxLease lease, CancellationTokenSource execution, CancellationToken stop)
    {
        try
        {
            using var timer = new PeriodicTimer(options.LeaseDuration / 3);
            while (await timer.WaitForNextTickAsync(stop))
            {
                // A separate scope avoids concurrently using the handler's DbContext.
                await using var scope = scopes.CreateAsyncScope();
                if (!await scope.ServiceProvider.GetRequiredService<IOutboxStore>()
                        .RenewAsync(lease, options.LeaseDuration, stop))
                {
                    await execution.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Outbox lease renewal failed for {MessageId}.", lease.Message.Id);
            await execution.CancelAsync();
        }
    }
}
