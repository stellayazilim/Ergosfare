namespace Ergosfare.Core.Test;

public class ResultAdapterTests
{
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TestResultAdapterServiceShouldThrowOnAddIncompatibleAdapter()
    {
        var resultAdapterService = new ResultAdapterService();

        Assert.Throws<ArgumentNullException>(() => resultAdapterService.AddAdapter(null));
    }
}