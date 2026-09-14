using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Reads a failure out of a result value without throwing it, so a result that carries its
/// error as data still reaches the exception-interceptor stage.
/// </summary>
/// <typeparam name="TResult">The closed result type this adapter reads.</typeparam>
/// <remarks>
/// Implement this for any carrier that represents failure as a value — the built-in
/// <see cref="Result"/> and <see cref="Result{TValue}"/>, or a third-party type such as
/// FluentResults or OneOf. An adapter is bound once per closed result type; a pipeline
/// whose result type has no adapter skips the probe entirely.
/// </remarks>
public interface IResultAdapter<TResult>
{
    /// <summary>
    /// Reads the failure carried by <paramref name="result"/>, if there is one.
    /// </summary>
    /// <param name="result">The result value to inspect.</param>
    /// <param name="exception">
    /// The carried failure when this method returns <c>true</c>; otherwise <c>null</c>.
    /// </param>
    /// <returns><c>true</c> when <paramref name="result"/> carries a failure.</returns>
    bool TryGetException(in TResult result, out Exception? exception);
}
