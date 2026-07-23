namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Non-generic marker root for final interceptors. Carries no members — the pipeline invokes
/// final interceptors exclusively through their typed contracts
/// (<see cref="IFinalInterceptor{TMessage, TResult}"/> /
/// <see cref="IAsyncFinalInterceptor{TMessage}"/> /
/// <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/>);
/// this root exists for storage typing and registration.
/// </summary>
public interface IFinalInterceptor;
