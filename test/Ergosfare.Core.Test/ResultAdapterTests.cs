namespace Ergosfare.Core.Test;

/// <summary>
/// Contains unit tests for <see cref="ResultAdapterService"/>, 
/// validating behavior when adding adapters.
/// </summary>
public class ResultAdapterTests
{
    /// <summary>
    /// Tests that <see cref="ResultAdapterService.AddAdapter"/> throws 
    /// <see cref="ArgumentNullException"/> when a null adapter is provided.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TestResultAdapterServiceShouldThrowOnAddIncompatibleAdapter()
    {
        var resultAdapterService = new ResultAdapterService();

        Assert.Throws<ArgumentNullException>(() => resultAdapterService.AddAdapter(null));
    }
}