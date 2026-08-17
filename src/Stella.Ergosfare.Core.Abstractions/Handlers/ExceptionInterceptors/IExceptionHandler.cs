
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Marks a type as an exception interceptor, for registration and storage.
/// </summary>
/// <remarks>
/// The interface declares no members. Exception interceptors are invoked through the typed
/// member of <see cref="IExceptionInterceptor{TMessage, TResult}"/>,
/// <see cref="IAsyncExceptionInterceptor{TMessage}"/> or
/// <see cref="IAsyncExceptionInterceptor{TMessage, TResult}"/>.
/// </remarks>
public interface IExceptionInterceptor;
