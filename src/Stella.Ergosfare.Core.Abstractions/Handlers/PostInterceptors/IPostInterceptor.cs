
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Marks a type as a post-interceptor, for registration and storage.
/// </summary>
/// <remarks>
/// The interface declares no members. Post-interceptors are invoked through the typed
/// member of <see cref="IPostInterceptor{TMessage, TResult}"/>,
/// <see cref="IAsyncPostInterceptor{TMessage}"/> or
/// <see cref="IAsyncPostInterceptor{TMessage, TResult}"/>.
/// </remarks>
public interface IPostInterceptor;
