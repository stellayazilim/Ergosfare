using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Extracts a value-carried failure out of a pipeline result of type
/// <typeparamref name="TResult"/> without throwing it — the bridge that lets result-pattern
/// values (the framework's own <see cref="Result"/>/<see cref="Result{TValue}"/>, or foreign
/// carriers such as FluentResults/OneOf) trigger the exception-interceptor stage.
/// </summary>
/// <typeparam name="TResult">The closed pipeline result type the adapter understands.</typeparam>
/// <remarks>
/// Typed on purpose: the previous object-based contract boxed every value-typed result on
/// every probe and re-discovered its target by <c>CanAdapt</c> checks. This shape binds per
/// closed result type — resolved once per pipeline, called devirtualized, and passed by
/// readonly reference so nothing is copied or boxed. A pipeline whose result type has no
/// adapter pays nothing at all.
/// </remarks>
public interface IResultAdapter<TResult>
{
    /// <summary>
    /// Attempts to extract a failure from <paramref name="result"/> without throwing.
    /// </summary>
    /// <param name="result">The pipeline result to inspect.</param>
    /// <param name="exception">The carried failure, when present.</param>
    /// <returns><c>true</c> when a failure was extracted; otherwise <c>false</c>.</returns>
    bool TryGetException(in TResult result, out Exception? exception);
}
