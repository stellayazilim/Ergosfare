namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The framework's default value-carried outcome: a successful <typeparamref name="TValue"/>,
/// or a failure wrapping the <see cref="Exception"/> that describes it — without throwing.
/// See <see cref="Result"/> for the zero-allocation rationale.
/// </summary>
/// <typeparam name="TValue">The successful payload type.</typeparam>
public readonly record struct Result<TValue> : INativeAdapterCarrier
{
    private readonly TValue? _value;

    private Result(TValue? value, Exception? exception)
    {
        _value = value;
        Exception = exception;
    }

    /// <inheritdoc />
    object INativeAdapterCarrier.NativeAdapter => ResultExceptionAdapter<TValue>.Instance;

    /// <summary>The carried failure, or <c>null</c> on success.</summary>
    public Exception? Exception { get; }

    /// <summary>Whether the outcome is a success.</summary>
    public bool IsSuccess => Exception is null;

    /// <summary>
    /// The successful payload. Throws <see cref="InvalidOperationException"/> when the
    /// outcome is a failure — read <see cref="Exception"/> or use
    /// <see cref="TryGetValue"/>/<see cref="GetValueOrDefault"/> on paths where failure is
    /// possible.
    /// </summary>
    public TValue Value
        => Exception is null
            ? _value!
            : throw new InvalidOperationException(
                $"The result is a failure ({Exception.GetType().Name}); it carries no value.");

    /// <summary>The successful payload, or <c>default</c> on failure. Never throws.</summary>
    public TValue? GetValueOrDefault() => _value;

    /// <summary>Pattern-friendly access: <c>true</c> with the payload on success.</summary>
    public bool TryGetValue(out TValue value)
    {
        value = _value!;
        return Exception is null;
    }

    /// <summary>A successful outcome carrying <paramref name="value"/>. Allocation-free.</summary>
    public static Result<TValue> Ok(TValue value) => new(value, null);

    /// <summary>
    /// A failed outcome carrying <paramref name="exception"/> — without throwing it, so no
    /// stack trace is captured and no unwind runs.
    /// </summary>
    /// <param name="exception">The failure to carry. Must not be null.</param>
    public static Result<TValue> Fail(Exception exception)
        => new(default, exception ?? throw new ArgumentNullException(nameof(exception)));

    /// <summary>Success values convert implicitly, keeping handler returns terse.</summary>
    public static implicit operator Result<TValue>(TValue value) => Ok(value);

    /// <inheritdoc />
    public override string ToString()
        => Exception is null ? $"Ok({_value})" : $"Fail({Exception.GetType().Name})";
}
