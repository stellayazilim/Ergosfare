namespace Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Represents a descriptor for a final interceptor handler.
/// </summary>
/// <remarks>
/// Inherits from <see cref="IHandlerDescriptor"/> and <see cref="IHasResultType"/>.
/// Contains metadata about the final interceptor, such as the handler type, 
/// associated message type, execution groups, weight, and result type.
/// </remarks>
public interface IFinalInterceptorDescriptor: IHandlerDescriptor, IHasResultType;