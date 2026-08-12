namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The framework's default adapter for its own <see cref="Result"/> carrier: a field read,
/// no boxing, no reflection. Bound automatically — never registered by hand.
/// </summary>
public sealed class ResultExceptionAdapter : IResultAdapter<Result>, IResultMaterializer<Result>
{
    /// <summary>The shared instance; the adapter is stateless.</summary>
    public static readonly ResultExceptionAdapter Instance = new();

    private ResultExceptionAdapter()
    {
    }

    /// <inheritdoc />
    public bool TryGetException(in Result result, out Exception? exception)
    {
        exception = result.Exception;
        return exception is not null;
    }

    /// <inheritdoc />
    public Result Materialize(Exception exception) => Result.Fail(exception);
}

/// <summary>
/// The framework's default adapter for <see cref="Result{TValue}"/>; see
/// <see cref="ResultExceptionAdapter"/>.
/// </summary>
/// <typeparam name="TValue">The carrier's payload type.</typeparam>
public sealed class ResultExceptionAdapter<TValue> : IResultAdapter<Result<TValue>>, IResultMaterializer<Result<TValue>>
{
    /// <summary>The shared instance; the adapter is stateless.</summary>
    public static readonly ResultExceptionAdapter<TValue> Instance = new();

    private ResultExceptionAdapter()
    {
    }

    /// <inheritdoc />
    public bool TryGetException(in Result<TValue> result, out Exception? exception)
    {
        exception = result.Exception;
        return exception is not null;
    }

    /// <inheritdoc />
    public Result<TValue> Materialize(Exception exception) => Result<TValue>.Fail(exception);
}
