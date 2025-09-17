
namespace Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Represents a descriptor for an exception interceptor handler.
/// </summary>
/// <remarks>
/// Inherits from <see cref="IHandlerDescriptor"/> and <see cref="IHasResultType"/>.
/// Contains metadata about the exception interceptor, such as the handler type, 
/// associated message type, execution groups, weight, and result type.
/// </remarks>
public interface IExceptionInterceptorDescriptor: IHandlerDescriptor, IHasResultType;
