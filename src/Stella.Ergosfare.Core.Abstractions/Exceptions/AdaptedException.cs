
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// An exception that carries the result value it was derived from, so code raising a
/// failure out of a result carrier can hand the carrier itself to whoever catches it.
/// </summary>
/// <remarks>
/// The framework never raises this exception. It is available to
/// <see cref="IResultAdapter{TResult}"/> implementations and to application code that turns
/// a failed result into a throw without losing the original value.
/// </remarks>
/// <param name="message">The exception message.</param>
/// <param name="originalResult">
/// The result value the failure was derived from. Cannot be <c>null</c>.
/// </param>
/// <exception cref="ArgumentNullException"><paramref name="originalResult"/> is <c>null</c>.</exception>
public sealed class AdaptedException(string message, object originalResult)
    : Exception(message)
{
    /// <summary>
    /// The result value this exception was derived from, held by reference.
    /// </summary>
    public object OriginalResult { get; } = originalResult ?? throw new ArgumentNullException(nameof(originalResult));

    /// <summary>
    /// Returns <see cref="OriginalResult"/> cast to <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">The type the original result is expected to be.</typeparam>
    /// <returns>The original result.</returns>
    /// <exception cref="InvalidCastException">
    /// The original result is not a <typeparamref name="TResult"/>.
    /// </exception>
    public TResult GetOriginalResult<TResult>() where TResult : notnull
        => (TResult)OriginalResult;
}
