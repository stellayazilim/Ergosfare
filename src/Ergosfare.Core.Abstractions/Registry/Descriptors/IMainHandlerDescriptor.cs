using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Represents the descriptor for a main handler that processes a message
/// and produces a result of a specific type.
/// </summary>
/// <remarks>
/// This interface combines <see cref="IHandlerDescriptor"/> for handler metadata
/// with <see cref="IHasResultType"/> to indicate the type of result the handler produces.
/// Typically used to register and resolve main handlers in the message handling pipeline.
/// </remarks>
public interface  IMainHandlerDescriptor: IHandlerDescriptor, IHasResultType;