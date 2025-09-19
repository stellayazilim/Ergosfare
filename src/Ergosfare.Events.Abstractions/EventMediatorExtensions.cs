using Ergosfare.Contracts;

namespace Ergosfare.Events.Abstractions;


/// <summary>
/// Provides extension methods for <see cref="IEventMediator"/> to simplify event publishing.
/// </summary>
public static class EventMediatorExtensions
{

    /// <summary>
    /// Publishes the specified event asynchronously to all registered handlers.
    /// </summary>
    /// <param name="eventMediator">The <see cref="IEventMediator"/> used to publish the event.</param>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous publish operation.</returns>
    public static Task PublishAsync(this IEventMediator eventMediator, IEvent @event, CancellationToken cancellationToken = default)
    {
        return eventMediator.PublishAsync(@event, null, cancellationToken);
    }

    
    /// <summary>
    /// Publishes the specified event asynchronously to all registered handlers,
    /// restricting delivery to the specified groups.
    /// </summary>
    /// <param name="eventMediator">The <see cref="IEventMediator"/> used to publish the event.</param>
    /// <param name="event">The event to publish.</param>
    /// <param name="groups">
    /// An array of group names to filter which handlers will receive the event.
    /// Only handlers belonging to these groups will be invoked.
    /// </param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous publish operation.</returns>
    public static Task PublishAsync(this IEventMediator eventMediator, IEvent @event, string[] groups, CancellationToken cancellationToken = default)
    {
        return eventMediator.PublishAsync(@event,
            new EventMediationSettings
            {
                Filters =
                {
                    Groups = groups
                }
            },
            cancellationToken);
    }
}