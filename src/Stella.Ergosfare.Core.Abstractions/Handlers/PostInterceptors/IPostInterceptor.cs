
namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Non-generic marker root for post-interceptors. Carries no members — the pipeline invokes
/// post-interceptors exclusively through their typed contracts
/// (<see cref="IPostInterceptor{TMessage, TResult}"/> /
/// <see cref="IAsyncPostInterceptor{TMessage}"/> /
/// <see cref="IAsyncPostInterceptor{TMessage, TResult}"/>);
/// this root exists for storage typing and registration.
/// </summary>
public interface IPostInterceptor;
