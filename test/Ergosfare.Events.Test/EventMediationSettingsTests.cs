using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Test;

public class EventMediationSettingsTests
{

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
                Tags = ["Tag1", "Tag2"],
                HandlerPredicate = _ => false
            }
        };
        
        
        Func<Type, bool> Predicate = _ => false;
        
        settings.Filters.HandlerPredicate = Predicate;
        Assert.Equal(Predicate, settings.Filters.HandlerPredicate);
        Assert.Equal("value", settings.Items["Key"]);
        Assert.Equal(["Tag1", "Tag2"], settings.Filters.Tags);
    }
}