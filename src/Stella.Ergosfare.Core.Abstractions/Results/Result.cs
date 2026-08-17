namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The outcome of a pipeline that produces no payload: either success, or a failure
/// carrying the <see cref="Exception"/> that describes it.
/// </summary>
/// <remarks>
/// <para>
/// Returning a failure is not the same as throwing one. The exception is carried as data,
/// so no stack trace is captured and no unwind runs, and the type is a
/// <c>readonly record struct</c>, so neither outcome allocates.
/// </para>
/// <para>
/// The framework recognizes this type without any registration: a pipeline whose result
/// carries a failure enters the exception-interceptor stage exactly as a thrown failure
/// would, and an unhandled one is returned to the caller as a failed result rather than
/// being thrown.
/// </para>
/// </remarks>
public readonly record struct Result : INativeAdapterCarrier
{
    private Result(Exception? exception) => Exception = exception;

    /// <summary>
    /// The adapter that reads this carrier, named by the carrier itself.
    /// </summary>
    object INativeAdapterCarrier.NativeAdapter => ResultExceptionAdapter.Instance;

    /// <summary>
    /// The carried failure, or <c>null</c> on success.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Whether this outcome is a success.
    /// </summary>
    public bool IsSuccess => Exception is null;

    /// <summary>
    /// Returns a successful outcome.
    /// </summary>
    public static Result Ok() => default;

    /// <summary>
    /// Returns a failed outcome carrying <paramref name="exception"/>, without throwing it.
    /// </summary>
    /// <param name="exception">The failure to carry. Cannot be <c>null</c>.</param>
    /// <returns>The failed outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <c>null</c>.</exception>
    public static Result Fail(Exception exception)
        => new(exception ?? throw new ArgumentNullException(nameof(exception)));

    /// <summary>
    /// Returns <c>Ok</c>, or <c>Fail</c> with the carried exception's type name.
    /// </summary>
    public override string ToString()
        => Exception is null ? "Ok" : $"Fail({Exception.GetType().Name})";
}
