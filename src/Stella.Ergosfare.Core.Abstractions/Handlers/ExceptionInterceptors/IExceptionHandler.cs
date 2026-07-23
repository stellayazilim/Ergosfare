namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Non-generic marker root for exception interceptors. Carries no members — the pipeline
/// invokes exception interceptors exclusively through their typed contracts
/// (<see cref="IExceptionInterceptor{TMessage, TResult}"/> /
/// <see cref="IAsyncExceptionInterceptor{TMessage}"/> /
/// <see cref="IAsyncExceptionInterceptor{TMessage, TResult}"/>);
/// this root exists for storage typing and registration.
/// </summary>
public interface IExceptionInterceptor;
