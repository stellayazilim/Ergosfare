namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Marks a type as a final interceptor, for registration and storage.
/// </summary>
/// <remarks>
/// The interface declares no members. Final interceptors are invoked through the typed
/// member of <see cref="IFinalInterceptor{TMessage, TResult}"/>,
/// <see cref="IAsyncFinalInterceptor{TMessage}"/> or
/// <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/>.
/// </remarks>
public interface IFinalInterceptor;
