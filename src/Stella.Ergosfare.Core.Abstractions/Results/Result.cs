namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The framework's default value-carried outcome for pipelines without a payload: success,
/// or a failure wrapping the <see cref="Exception"/> that describes it — without throwing.
/// </summary>
/// <remarks>
/// <para>
/// A <c>readonly record struct</c> by design: constructing and returning one allocates
/// nothing, and — because .NET captures an exception's stack trace at <c>throw</c> time,
/// not at construction — a handler that returns <see cref="Fail"/> instead of throwing
/// skips the stack capture and the two-pass unwind entirely. The success path costs a
/// single field read to check.
/// </para>
/// <para>
/// The framework recognizes this type natively: a pipeline whose result carries a failure
/// routes it to the exception-interceptor stage exactly as a thrown exception would be,
/// via <see cref="Results.ResultExceptionAdapter"/> — no user adapter registration needed.
/// </para>
/// </remarks>
public readonly record struct Result : INativeAdapterCarrier
{
    private Result(Exception? exception) => Exception = exception;

    /// <inheritdoc />
    object INativeAdapterCarrier.NativeAdapter => ResultExceptionAdapter.Instance;

    /// <summary>The carried failure, or <c>null</c> on success.</summary>
    public Exception? Exception { get; }

    /// <summary>Whether the outcome is a success.</summary>
    public bool IsSuccess => Exception is null;

    /// <summary>The successful outcome. Allocation-free.</summary>
    public static Result Ok() => default;

    /// <summary>
    /// A failed outcome carrying <paramref name="exception"/> — without throwing it, so no
    /// stack trace is captured and no unwind runs.
    /// </summary>
    /// <param name="exception">The failure to carry. Must not be null.</param>
    public static Result Fail(Exception exception)
        => new(exception ?? throw new ArgumentNullException(nameof(exception)));

    /// <inheritdoc />
    public override string ToString()
        => Exception is null ? "Ok" : $"Fail({Exception.GetType().Name})";
}
