using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Contains unit tests for <see cref="EventMediationSettings"/>,
/// validating that handler filters and settings are properly set and retrieved.
/// </summary>
public class EventMediationSettingsTests
{
    /// <summary>
    /// Tests that the settings' properties can be correctly set and retrieved.
    /// </summary>
    /// <remarks>
    /// The per-publish handler predicate this test also covered is gone: groups are the only
    /// handler filter now, and they say the same thing declaratively — which is what lets the
    /// composition be compiled instead of tested per handler on every publish.
    /// </remarks>
    [Fact]
    [Trait("TestCategory", "unit")]
    public void ShouldFiltersAndItemsSet()
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
            }
        };

        Assert.Equal("value", settings.Items["Key"]);
        Assert.Equal(["Tag1", "Tag2"], settings.Filters.Groups);
    }
}
