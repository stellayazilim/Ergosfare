
using Stella.Ergosfare.Test.Fixtures;

namespace Stella.Ergosfare.Core.Test.ResultAdapters;

/// <summary>
/// Unit tests for <see cref="ResultAdapterService"/> using <see cref="ResultAdapterFixtures"/>.
/// Covers adapter registration, retrieval, and exception lookup scenarios.
/// </summary>
public class ResultAdapterServiceTests : IClassFixture<ResultAdapterFixtures>
{
    private readonly ResultAdapterFixtures _fixture;

    /// <summary>
    /// Initializes a new instance of the test class using the shared fixture.
    /// </summary>
    /// <param name="fixture">The fixture providing adapters and service instance.</param>
    public ResultAdapterServiceTests(ResultAdapterFixtures fixture)
    {
        _fixture = fixture.New; // get a clean instance each time
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterService.AddAdapter"/> stores
    /// adapters and that <see cref="ResultAdapterService.GetAdapters"/> returns them.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void AddAdapter_ShouldStoreAdapter()
    {
        var adapter = new ResultAdapterFixtures.AlwaysAdaptAdapter();
        _fixture.ResultAdapterService.AddAdapter(adapter);

        var adapters = _fixture.ResultAdapterService.GetAdapters();

        Assert.Contains(adapter, adapters);
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterService.AddAdapter"/> throws
    /// <see cref="ArgumentNullException"/> when the adapter argument is null.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AddAdapter_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => _fixture.ResultAdapterService.AddAdapter(null!));
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterService.LookupException"/> returns
    /// null if the provided result is null.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void LookupException_ShouldReturnNullForNullResult()
    {
        var ex = _fixture.ResultAdapterService.LookupException(null);

        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterService.LookupException"/> returns
    /// the exception from the first adapter that claims it can adapt the result.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void LookupException_ShouldReturnExceptionFromAdapter()
    {
        _fixture.ResultAdapterService.AddAdapter(new ResultAdapterFixtures.AlwaysAdaptAdapter());

        var ex = _fixture.ResultAdapterService.LookupException("anything");

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("adapted", ex?.Message);
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterService.LookupException"/> returns
    /// null when the adapter can adapt but does not produce an exception.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void LookupException_ShouldReturnNullIfAdapterFails()
    {
        _fixture.ResultAdapterService.AddAdapter(new ResultAdapterFixtures.NullExceptionAdapter());

        var ex = _fixture.ResultAdapterService.LookupException("input");

        Assert.Null(ex);
    }

    /// <summary>
    /// Verifies that <see cref="ResultAdapterService.LookupException"/> skips
    /// adapters that cannot adapt and continues to later ones.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void LookupException_ShouldSkipNonSupportingAdapters()
    {
        _fixture.ResultAdapterService.AddAdapter(new ResultAdapterFixtures.NeverAdaptAdapter());
        _fixture.ResultAdapterService.AddAdapter(new ResultAdapterFixtures.AlwaysAdaptAdapter());

        var ex = _fixture.ResultAdapterService.LookupException("foo");

        Assert.IsType<InvalidOperationException>(ex);
    }
}