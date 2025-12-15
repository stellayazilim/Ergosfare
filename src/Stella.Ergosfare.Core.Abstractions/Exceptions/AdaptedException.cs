using System;

namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Exception wrapper used by result adapters to surface errors without
/// directly throwing the original result type.
/// </summary>
/// <param name="message">Human-readable message for the exception.</param>
/// <param name="originalResult">The original result object that was adapted.</param>
public sealed class AdaptedException(string message, object originalResult) 
    : Exception(message)
{
    /// <summary>
    /// Gets the original result object that triggered this exception.
    /// Stored by reference, not copied.
    /// </summary>
    public object OriginalResult { get; } = originalResult ?? throw new ArgumentNullException(nameof(originalResult));

    /// <summary>
    /// Retrieves the original result as a strongly typed value.
    /// </summary>
    /// <typeparam name="TResult">The expected type of the original result.</typeparam>
    /// <returns>The original result cast to <typeparamref name="TResult"/>.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown if the original result cannot be cast to the requested type.
    /// </exception>
    public TResult GetOriginalResult<TResult>() where TResult : notnull
        => (TResult)OriginalResult;
}
