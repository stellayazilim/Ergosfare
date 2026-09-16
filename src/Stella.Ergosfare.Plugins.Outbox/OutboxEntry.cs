using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Plugins.Abstractions;

[assembly: ErgosfarePlugin("OutboxContext")]
[assembly: System.Reflection.AssemblyMetadata("ErgosfareSourceGeneratorForceScanReferences", "true")]

namespace Stella.Ergosfare.Plugins.Outbox;

/// <summary>A normal command requesting storage of a message for later delivery.</summary>
public sealed record OutboxEntry<T>(T Message) : ICommand where T : notnull;

/// <summary>Delivery envelope, distinct from publishing the user's message directly.</summary>
public sealed record OutboxEvent<T>(T Message) : IEvent where T : notnull;

/// <summary>An event handler executed by an outbox delivery pipeline.</summary>
[ExcludeFromDiscovery]
public interface IOutboxHandler<T> : IEventHandler<OutboxEvent<T>>,
    IOutboxPlanDependency<OutboxSaveHandler<T>> where T : notnull;

/// <summary>
/// Declares the closed save participant in the handler's interface graph. The existing
/// generator reads closed generic interface arguments, including across assembly references.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public interface IOutboxPlanDependency<T>;

/// <summary>Stores a message through the provider registered in the current DI scope.</summary>
public sealed class OutboxSaveHandler<T>(IOutboxStore store, OutboxOptions options)
    : ICommandHandler<OutboxEntry<T>> where T : notnull
{
    public ValueTask HandleAsync(OutboxEntry<T> command, ErgosfareContext context)
    {
        ArgumentNullException.ThrowIfNull(command.Message);
        var registration = options.Find(typeof(T));
        var record = new OutboxMessage(Guid.NewGuid(), registration.Contract,
            registration.Serialize(command.Message));
        return store.AppendAsync(record, context.CancellationToken);
    }
}

/// <summary>Attaches the calling scope's mediator without changing the core context.</summary>
public sealed class OutboxContextBinding
{
    internal static readonly object Key = new();

    [PipelineInvokable(Hook.Start)]
    public void Bind<T>(T message, ErgosfareContext context, ICommandMediator commands)
        => context.Items[Key] = commands;
}

public static class OutboxContextExtensions
{
    /// <summary>Awaits storage participation, not delivery or the caller's transaction commit.</summary>
    public static async ValueTask EnqueueOutboxAsync<T>(this ErgosfareContext context, T message)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        if (!context.Items.TryGetValue(OutboxContextBinding.Key, out var value)
            || value is not ICommandMediator commands)
            throw new InvalidOperationException("Outbox context is not bound. Configure AddOutboxPlugin and generate the calling pipeline with plugin discovery enabled.");

        using var child = context.CreateScope();
        await commands.SendAsync(new OutboxEntry<T>(message), child.Context);
    }
}
