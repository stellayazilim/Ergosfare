namespace Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

/// <summary>
/// Describes a pre-interceptor handler for a message type.
/// </summary>
/// <remarks>
/// Pre-interceptors are invoked before the main handler executes, 
/// allowing for validation, modification of the message, or short-circuiting logic.
/// This descriptor includes information about the handler type, 
/// the message type it applies to, its weight, and groups.
/// </remarks>
public interface IPreInterceptorDescriptor: IHandlerDescriptor;