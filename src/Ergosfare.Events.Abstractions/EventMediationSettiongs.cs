namespace Ergosfare.Events.Abstractions;

/// <summary>
///     Represents the settings used for event mediation.
/// </summary>
public sealed class EventMediationSettings
{

    public bool ThrowIfNoHandlerFound { get; init; } = false;


    public EventMediationFilters Filters { get; } = new();

    
    public IDictionary<object, object?> Items { get; init; } = new Dictionary<object, object?>();

    public sealed class EventMediationFilters
    {
   
        public IEnumerable<string> Groups { get; set; } = new List<string>();


        public Func<Type, bool> HandlerPredicate { get; set; } = _ => true;
    }
}