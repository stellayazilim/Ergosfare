using Ergosfare.Queries.Abstractions;

namespace Ergosfare.Queries.Test;

public class QueryMediationSettingsTests
{
    private readonly QueryMediationSettings _queryMediationSettings
        = new QueryMediationSettings()
        {
            Items = new Dictionary<object, object?>()
            {
                { "Item1", "Item1 value"}
            },
            Filters = { Tags = ["Tag1", "Tag2"]}
        };

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void QueryMediationSettingsTest()
    {
        Assert.NotNull(_queryMediationSettings);
        Assert.Equal("Item1 value", _queryMediationSettings.Items["Item1"]);
        Assert.Equal(["Tag1", "Tag2"], _queryMediationSettings.Filters.Tags);
    }
    
}