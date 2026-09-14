namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// Reads and builds the framework's <see cref="Result"/> carrier. Bound automatically —
/// never registered by hand.
/// </summary>
public sealed class ResultExceptionAdapter : IResultAdapter<Result>, IResultMaterializer<Result>
{
    /// <summary>
    /// The shared instance; the adapter holds no state.
    /// </summary>
    public static readonly ResultExceptionAdapter Instance = new();

    private ResultExceptionAdapter()
    {
    }

    /// <summary>
    /// Reads the failure carried by <paramref name="result"/>, if there is one.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="exception">The carried failure, or <c>null</c> on success.</param>
    /// <returns><c>true</c> when the result carries a failure.</returns>
    public bool TryGetException(in Result result, out Exception? exception)
    {
        exception = result.Exception;
        return exception is not null;
    }

    /// <summary>
    /// Builds the failed result carrying <paramref name="exception"/>.
    /// </summary>
    /// <param name="exception">The failure to carry.</param>
    /// <returns>The failed result.</returns>
    public Result Materialize(Exception exception) => Result.Fail(exception);
}

/// <summary>
/// Reads and builds the framework's <see cref="Result{TValue}"/> carrier; the payload
/// counterpart of <see cref="ResultExceptionAdapter"/>.
/// </summary>
/// <typeparam name="TValue">The carrier's payload type.</typeparam>
public sealed class ResultExceptionAdapter<TValue> : IResultAdapter<Result<TValue>>, IResultMaterializer<Result<TValue>>
{
    /// <summary>
    /// The shared instance; the adapter holds no state.
    /// </summary>
    public static readonly ResultExceptionAdapter<TValue> Instance = new();

    private ResultExceptionAdapter()
    {
    }

    /// <summary>
    /// Reads the failure carried by <paramref name="result"/>, if there is one.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="exception">The carried failure, or <c>null</c> on success.</param>
    /// <returns><c>true</c> when the result carries a failure.</returns>
    public bool TryGetException(in Result<TValue> result, out Exception? exception)
    {
        exception = result.Exception;
        return exception is not null;
    }

    /// <summary>
    /// Builds the failed result carrying <paramref name="exception"/>.
    /// </summary>
    /// <param name="exception">The failure to carry.</param>
    /// <returns>The failed result.</returns>
    public Result<TValue> Materialize(Exception exception) => Result<TValue>.Fail(exception);
}
