using Ergosfare.Commands.Abstractions;

namespace Ergosfare.Command.Test;

public class CommandMediationSettingsTests
{
    private readonly CommandMediationSettings _commandMediationSettings =  new CommandMediationSettings()
    {
        Items = new Dictionary<object, object?>()
        {
            {
                "Item1", "Item1 value"
            }
        },
        
        Filters =
        {
            Tags =
            [
                "Tag1", "Tag2"
            ]
        }
    };


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void  CommandMediationSettingsTest()
    {
        
        Assert.Equal("Item1 value", _commandMediationSettings.Items["Item1"]);
        Assert.Equal(["Tag1", "Tag2"], _commandMediationSettings.Filters.Tags);
    }
}