namespace Stella.Ergosfare.Core.Abstractions.Handlers;



/// <summary>
///     Marker interface identifying message handlers for registration and storage. The
///     pipeline never invokes handlers through this interface — dispatch goes through the
///     typed members of <see cref="IHandler{TMessage, TResult}"/> (synchronous) or
///     <c>IAsyncHandler</c> (asynchronous); the object-typed bridge member was removed with
///     the v2 executor dispatch.
/// </summary>
public interface IHandler;
