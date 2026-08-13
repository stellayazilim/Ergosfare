namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
///     Represents the settings used for event mediation.
/// </summary>
public sealed class EventMediationSettings
{

    /// <summary>
    /// Gets or sets a value indicating whether an exception should be thrown
    /// if no handlers are found for a published event.
    /// </summary>
    /// <remarks>
    /// Covers every way a publish can reach nobody: an event type absent from the
    /// registry, a registered one with no handlers, and a registered one whose handlers
    /// are all filtered out. Left unset, a publish that reaches nobody is a silent no-op —
    /// fire-and-forget is the point of an event, and a publisher that must know whether
    /// anyone listened wants this flag.
    /// </remarks>
    public bool ThrowIfNoHandlerFound { get; init; }


    /// <summary>
    /// Gets the filters that control which handlers will receive the event.
    /// </summary>
    public EventMediationFilters Filters { get; } = new();

    
    /// <summary>
    /// Gets or sets a collection of arbitrary key/value items that can be used
    /// to pass contextual information through the event mediation pipeline.
    /// </summary>
    public IDictionary<object, object?> Items { get; init; } = new Dictionary<object, object?>();

    /// <summary>
    /// Represents the filtering options applied during event mediation to select
    /// which handlers should be invoked.
    /// </summary>
    /// <remarks>
    /// Groups are the only handler filter, deliberately. A per-publish predicate over handler
    /// types used to live here too; it was the one setting a compiled plan could not honour —
    /// an arbitrary delegate is unknowable until the call — and it said nothing groups cannot
    /// say declaratively, where the composition can be baked instead of tested per handler.
    /// </remarks>
    public sealed class EventMediationFilters
    {
        /// <summary>
        /// Gets or sets the collection of group names used to filter event handlers.
        /// Only handlers belonging to these groups will receive the event.
        /// </summary>
        public IEnumerable<string> Groups { get; set; } = new List<string>();
    }
}