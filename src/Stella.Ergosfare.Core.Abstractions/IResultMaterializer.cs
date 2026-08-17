namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Builds a failed result value from an exception — the inverse of
/// <see cref="IResultAdapter{TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The closed result type this materializer produces.</typeparam>
/// <remarks>
/// <para>
/// Implementing this alongside <see cref="IResultAdapter{TResult}"/> changes how the
/// pipeline settles for that result type: an exception thrown inside the pipeline is
/// caught and turned into a failed result instead of reaching the caller, and a carried
/// failure that no interceptor handled flows out as the returned result rather than being
/// rethrown.
/// </para>
/// <para>
/// It is a separate contract because not every carrier can absorb an arbitrary exception.
/// A result type whose adapter does not implement this keeps the default behavior: an
/// unhandled failure is thrown to the caller. The built-in
/// <see cref="Results.Result"/> and <see cref="Results.Result{TValue}"/> adapters implement it.
/// </para>
/// </remarks>
public interface IResultMaterializer<out TResult>
{
    /// <summary>
    /// Builds the failed result carrying <paramref name="exception"/>.
    /// </summary>
    /// <param name="exception">The failure to carry.</param>
    /// <returns>A failed <typeparamref name="TResult"/> carrying <paramref name="exception"/>.</returns>
    TResult Materialize(Exception exception);
}
