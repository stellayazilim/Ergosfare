using Stella.Ergosfare.Commands.Abstractions;

namespace Stella.Ergosfare.Command.Test;

public class CommandMediationSettingsTests
{
    /// <summary>
    /// Represents the command mediation settings used to configure items and filters
    /// for command handling and processing.
    /// </summary>
    private readonly CommandMediationSettings _commandMediationSettings =  new CommandMediationSettings()
    {
        Items = new Dictionary<object, object?>() {  {  "Item1", "Item1 value" } },
        
        Filters =  { Groups =  [ "Tag1", "Tag2" ] }
    };

    /// <summary>
    /// Tests the <see cref="CommandMediationSettings"/> initialization and verifies
    /// that the <see cref="CommandMediationSettings.Items"/> dictionary and
    /// <see cref="CommandMediationSettings.CommandMediationFilters.Groups"/> list are correctly populated.
    /// </summary>
    /// <remarks>
    /// This is a unit test marked with the "Unit" and "Coverage" traits for categorization.
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void  CommandMediationSettingsTest()
    {
        
        Assert.Equal("Item1 value", _commandMediationSettings.Items["Item1"]);
        Assert.Equal(["Tag1", "Tag2"], _commandMediationSettings.Filters.Groups);
    }
}