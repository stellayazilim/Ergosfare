
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Narrows an exception interceptor to <typeparamref name="TException"/> and the types
/// derived from it, the way a <c>catch</c> clause does.
/// </summary>
/// <typeparam name="TException">The exception type this interceptor accepts.</typeparam>
/// <remarks>
/// Implement this alongside an exception-interceptor contract; the test is supplied here,
/// so there is nothing to write. Generated pipelines read
/// <typeparamref name="TException"/> off this contract and emit the same test, so filtering
/// behaves identically however the interceptor was registered.
/// </remarks>
public interface IExceptionInterceptorFilter<TException> : IExceptionInterceptorFilter
    where TException : Exception
{
    /// <summary>
    /// Accepts <paramref name="exception"/> when it is a <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="exception">The failure the pipeline raised.</param>
    /// <returns><c>true</c> when the failure is a <typeparamref name="TException"/>.</returns>
    bool IExceptionInterceptorFilter.Matches(Exception exception) => exception is TException;
}
