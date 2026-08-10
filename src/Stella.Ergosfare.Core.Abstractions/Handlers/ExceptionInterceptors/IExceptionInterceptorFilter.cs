
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// The erased probe an exception interceptor carries when it accepts only some exceptions.
/// The exception stage asks every interceptor that implements it whether the thrown
/// exception is one it accepts, runs only those that answer yes, and rethrows the original
/// exception unwrapped when none does.
/// </summary>
/// <remarks>
/// An interceptor that does not implement this contract accepts every exception — the
/// untyped facades keep their unfiltered behavior with no opt-in.
/// <para>
/// The probe is deliberately separate from the dispatch contracts
/// (<see cref="IExceptionInterceptor{TMessage, TResult}"/> and the asynchronous pair):
/// filtering is orthogonal to which typed member the stage invokes, so a filtered
/// interceptor is dispatched through exactly the same arm as an unfiltered one.
/// </para>
/// </remarks>
public interface IExceptionInterceptorFilter
{
    /// <summary>
    /// Determines whether this interceptor accepts the thrown exception.
    /// </summary>
    /// <param name="exception">The exception the pipeline threw.</param>
    /// <returns><c>true</c> when the interceptor should run for this exception; otherwise <c>false</c>.</returns>
    bool Matches(Exception exception);
}
