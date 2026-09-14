
namespace Stella.Ergosfare.Core.Abstractions.Handlers;

/// <summary>
/// Marks a type as a pre-interceptor, for registration and storage.
/// </summary>
/// <remarks>
/// The interface declares no members. Pre-interceptors are invoked through the typed member
/// of <see cref="IPreInterceptor{TMessage}"/> or
/// <see cref="IAsyncPreInterceptor{TMessage}"/>.
/// </remarks>
public interface IPreInterceptor;
