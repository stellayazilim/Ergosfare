
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Narrows an exception interceptor to the failures it accepts.
/// </summary>
/// <remarks>
/// <para>
/// The exception stage asks each interceptor that implements this contract whether it
/// accepts the failure, and skips the ones that say no — a skipped interceptor does not
/// count as having handled anything, so a failure every interceptor rejects stays
/// unhandled and settles as if no interceptor were registered at all.
/// </para>
/// <para>
/// An interceptor that does not implement this contract accepts every failure. Prefer the
/// typed <see cref="IExceptionInterceptorFilter{TException}"/>, which implements
/// <see cref="Matches"/> for you; implement this one directly only for a test the exception
/// type alone cannot express.
/// </para>
/// </remarks>
public interface IExceptionInterceptorFilter
{
    /// <summary>
    /// Reports whether this interceptor accepts <paramref name="exception"/>.
    /// </summary>
    /// <param name="exception">The failure the pipeline raised.</param>
    /// <returns><c>true</c> to run this interceptor for the failure.</returns>
    bool Matches(Exception exception);
}
