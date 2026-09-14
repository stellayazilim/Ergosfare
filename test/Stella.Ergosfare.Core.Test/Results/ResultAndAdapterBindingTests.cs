using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Test.Results;

public class ResultAndAdapterBindingTests
{
    [Fact]
    public void ResultCarriers_ExposeSuccessAndFailure()
    {
        var ok = Result.Ok();
        Assert.True(ok.IsSuccess);
        Assert.Null(ok.Exception);

        var failure = new InvalidOperationException("boom");
        var fail = Result.Fail(failure);
        Assert.False(fail.IsSuccess);
        Assert.Same(failure, fail.Exception);

        Result<int> okValue = 42; // implicit success conversion
        Assert.True(okValue.IsSuccess);
        Assert.Equal(42, okValue.Value);
        Assert.True(okValue.TryGetValue(out var value) && value == 42);

        var failValue = Result<int>.Fail(failure);
        Assert.False(failValue.IsSuccess);
        Assert.Same(failure, failValue.Exception);
        Assert.False(failValue.TryGetValue(out _));
        Assert.Equal(0, failValue.GetValueOrDefault());
        Assert.Throws<InvalidOperationException>(() => failValue.Value);
    }

    [Fact]
    public void NativeCarriers_BindTheirBuiltInAdapters()
    {
        var voidAdapter = ResultExceptionAdapter.Instance;
        Assert.NotNull(voidAdapter);

        var failure = new InvalidOperationException("boom");
        Assert.True(voidAdapter.TryGetException(Result.Fail(failure), out var carried));
        Assert.Same(failure, carried);
        Assert.False(voidAdapter.TryGetException(Result.Ok(), out _));

        var valueAdapter = ResultExceptionAdapter<string>.Instance;
        Assert.NotNull(valueAdapter);
        Assert.True(valueAdapter.TryGetException(Result<string>.Fail(failure), out carried));
        Assert.Same(failure, carried);
        Assert.False(valueAdapter.TryGetException(Result<string>.Ok("ok"), out _));
    }

    [Fact]
    public void NativeAdapters_MaterializeFailuresIntoFailedCarriers()
    {
        var failure = new InvalidOperationException("boom");

        var voidMaterializer = Assert.IsAssignableFrom<IResultMaterializer<Result>>(
            ResultExceptionAdapter.Instance);
        var materialized = voidMaterializer.Materialize(failure);
        Assert.False(materialized.IsSuccess);
        Assert.Same(failure, materialized.Exception);

        var valueMaterializer = Assert.IsAssignableFrom<IResultMaterializer<Result<string>>>(
            ResultExceptionAdapter<string>.Instance);
        var materializedValue = valueMaterializer.Materialize(failure);
        Assert.False(materializedValue.IsSuccess);
        Assert.Same(failure, materializedValue.Exception);

    }
}
