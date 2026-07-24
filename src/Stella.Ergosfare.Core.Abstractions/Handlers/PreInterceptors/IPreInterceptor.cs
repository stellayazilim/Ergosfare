
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Non-generic marker root for pre-interceptors. Carries no members — the pipeline invokes
/// pre-interceptors exclusively through their typed contracts
/// (<see cref="IPreInterceptor{TMessage}"/> / <see cref="IAsyncPreInterceptor{TMessage}"/>);
/// this root exists for storage typing and registration.
/// </summary>
public interface IPreInterceptor;
