namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The outcome of a pipeline that produces a <typeparamref name="TValue"/>: either that
/// value, or a failure carrying the <see cref="Exception"/> that describes it.
/// </summary>
/// <typeparam name="TValue">The payload type of a successful outcome.</typeparam>
/// <remarks>
/// The payload counterpart of <see cref="Result"/>, with the same properties: failures are
/// carried as data rather than thrown, neither outcome allocates, and the framework reads
/// the carrier without any registration.
/// </remarks>
public readonly record struct Result<TValue> : INativeAdapterCarrier
{
    private readonly TValue? _value;

    private Result(TValue? value, Exception? exception)
    {
        _value = value;
        Exception = exception;
    }

    /// <summary>
    /// The adapter that reads this carrier, named by the carrier itself.
    /// </summary>
    object INativeAdapterCarrier.NativeAdapter => ResultExceptionAdapter<TValue>.Instance;

    /// <summary>
    /// The carried failure, or <c>null</c> on success.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Whether this outcome is a success.
    /// </summary>
    public bool IsSuccess => Exception is null;

    /// <summary>
    /// The payload of a successful outcome.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The outcome is a failure. Use <see cref="TryGetValue"/> or
    /// <see cref="GetValueOrDefault"/> where failure is possible.
    /// </exception>
    public TValue Value
        => Exception is null
            ? _value!
            : throw new InvalidOperationException(
                $"The result is a failure ({Exception.GetType().Name}); it carries no value.");

    /// <summary>
    /// Returns the payload, or the default of <typeparamref name="TValue"/> on failure.
    /// </summary>
    /// <returns>The payload, or its default.</returns>
    public TValue? GetValueOrDefault() => _value;

    /// <summary>
    /// Reads the payload when this outcome is a success.
    /// </summary>
    /// <param name="value">
    /// The payload when this method returns <c>true</c>; otherwise the default of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> when the outcome is a success.</returns>
    public bool TryGetValue(out TValue value)
    {
        value = _value!;
        return Exception is null;
    }

    /// <summary>
    /// Returns a successful outcome carrying <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The payload.</param>
    /// <returns>The successful outcome.</returns>
    public static Result<TValue> Ok(TValue value) => new(value, null);

    /// <summary>
    /// Returns a failed outcome carrying <paramref name="exception"/>, without throwing it.
    /// </summary>
    /// <param name="exception">The failure to carry. Cannot be <c>null</c>.</param>
    /// <returns>The failed outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <c>null</c>.</exception>
    public static Result<TValue> Fail(Exception exception)
        => new(default, exception ?? throw new ArgumentNullException(nameof(exception)));

    /// <summary>
    /// Converts a payload into a successful outcome, so a handler can return the value
    /// directly.
    /// </summary>
    /// <param name="value">The payload.</param>
    public static implicit operator Result<TValue>(TValue value) => Ok(value);

    /// <summary>
    /// Returns <c>Ok</c> with the payload, or <c>Fail</c> with the carried exception's type
    /// name.
    /// </summary>
    public override string ToString()
        => Exception is null ? $"Ok({_value})" : $"Fail({Exception.GetType().Name})";
}
