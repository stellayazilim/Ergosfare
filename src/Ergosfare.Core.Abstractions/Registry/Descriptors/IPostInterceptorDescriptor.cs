namespace Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Describes a post-interceptor handler for a message type.
/// </summary>
/// <remarks>
/// Post-interceptors are invoked after the main handler executes, 
/// allowing for post-processing, transformations, or side effects.
/// This descriptor includes information about the handler type, 
/// the message type it applies to, its weight, groups, and result type.
/// </remarks>
public interface IPostInterceptorDescriptor:  IHandlerDescriptor, IHasResultType;