namespace Stella.Ergosfare.Core.Abstractions.Handlers;



/// <summary>
/// Marks a type as a main message handler, for registration and storage.
/// </summary>
/// <remarks>
/// The interface declares no members and the pipeline never invokes anything through it.
/// Handlers are invoked through the typed member of the contract they implement —
/// <see cref="IHandler{TMessage, TResult}"/>, <see cref="IAsyncHandler{TMessage}"/>,
/// <see cref="IAsyncHandler{TMessage, TResult}"/> or
/// <see cref="IStreamHandler{TMessage, TResult}"/>.
/// </remarks>
public interface IHandler;
