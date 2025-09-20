using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Test;

/// <summary>
/// Contains unit tests for <see cref="EventMediationSettings"/>, 
/// validating that handler filters and settings are properly set and retrieved.
/// </summary>
public class EventMediationSettingsTests
{
    
    /// <summary>
    /// Tests that the <see cref="EventMediationSettings.Filters.HandlerPredicate"/> 
    /// and other properties can be correctly set and retrieved.
    /// </summary>
    [Fact]
    [Trait("TestCategory", "unit")]
    public void ShouldHandlerPredicateSet()
    {
        var settings = new EventMediationSettings
        {
            Items = new Dictionary<object, object?>()
            {
                {"Key", "value"}
            },
            Filters =
            {
                Groups = ["Tag1", "Tag2"],
                HandlerPredicate = _ => false
            }
        };
        
        
        Func<Type, bool> Predicate = _ => false;
        
        settings.Filters.HandlerPredicate = Predicate;
        Assert.Equal(Predicate, settings.Filters.HandlerPredicate);
        Assert.Equal("value", settings.Items["Key"]);
        Assert.Equal(["Tag1", "Tag2"], settings.Filters.Groups);
    }
}