using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// Contains unit tests for <see cref="QueryMediationSettings"/>,
/// verifying that items and filter groups are correctly configured.
/// </summary>
public class QueryMediationSettingsTests
{
    /// <summary>
    /// An instance of <see cref="QueryMediationSettings"/> used for testing.
    /// Initialized with sample items and filter groups.
    /// </summary>
    private readonly QueryMediationSettings _queryMediationSettings
        = new QueryMediationSettings()
        {
            Items = new Dictionary<object, object?>()
            {
                { "Item1", "Item1 value"}
            },
            Filters = { Groups = ["Tag1", "Tag2"]}
        };

    /// <summary>
    /// Tests that <see cref="QueryMediationSettings"/> is initialized correctly
    /// with the expected items and filter groups.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void QueryMediationSettingsTest()
    {
        Assert.NotNull(_queryMediationSettings);
        Assert.Equal("Item1 value", _queryMediationSettings.Items["Item1"]);
        Assert.Equal(["Tag1", "Tag2"], _queryMediationSettings.Filters.Groups);
    }
    
}