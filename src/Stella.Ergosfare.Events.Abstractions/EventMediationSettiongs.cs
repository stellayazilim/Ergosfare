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
    public bool ThrowIfNoHandlerFound { get; init; } = false;


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
    public sealed class EventMediationFilters
    {
   
        /// <summary>
        /// Gets or sets the collection of group names used to filter event handlers.
        /// Only handlers belonging to these groups will receive the event.
        /// </summary>
        public IEnumerable<string> Groups { get; set; } = new List<string>();


        /// <summary>
        /// Gets or sets a predicate function to filter handlers by their type.
        /// By default, all handler types are included.
        /// </summary>
        public Func<Type, bool> HandlerPredicate { get; set; } = _ => true;
    }
}