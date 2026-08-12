namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The inverse of <see cref="IResultAdapter{TResult}"/>: builds a failed
/// <typeparamref name="TResult"/> out of an exception. An adapter that also implements
/// this contract declares its carrier type fully value-based — a real throw inside the
/// pipeline is caught and materialized into a failed carrier instead of reaching the
/// caller, and an unhandled carried failure flows out as the result rather than being
/// rethrown. The framework's own <see cref="Results.Result"/>/<see cref="Results.Result{TValue}"/>
/// adapters implement it; a foreign carrier's adapter may opt in when the carrier can
/// represent an arbitrary exception.
/// </summary>
/// <typeparam name="TResult">The closed pipeline result type the materializer produces.</typeparam>
/// <remarks>
/// Deliberately separate from <see cref="IResultAdapter{TResult}"/>: every carrier can
/// surface a failure, but not every carrier can absorb one (a union type without an
/// exception arm extracts fine yet cannot materialize). Pipelines probe for this contract
/// once, next to the adapter binding — a carrier without it keeps the classic semantics:
/// an unhandled exception is rethrown to the caller.
/// </remarks>
public interface IResultMaterializer<out TResult>
{
    /// <summary>Builds the failed carrier representing <paramref name="exception"/>.</summary>
    /// <param name="exception">The failure to carry.</param>
    /// <returns>A failed <typeparamref name="TResult"/> carrying <paramref name="exception"/>.</returns>
    TResult Materialize(Exception exception);
}
