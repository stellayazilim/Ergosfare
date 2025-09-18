using System.Collections;
using System.Collections.Concurrent;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;


namespace Ergosfare.Core.Internal.Registry;



/// <summary>
/// Represents a registry that holds all message types and their associated handler descriptors.
/// </summary>
/// <remarks>
/// This class manages the registration and tracking of message types (such as commands, queries, events)
/// and their related handlers within the application.
/// 
/// It supports registering both message types directly and handlers, where handlers implicitly register
/// their associated message types. The registry maintains collections of:
/// <list type="bullet">
///   <item><description>All unique message descriptors.</description></item>
///   <item><description>All handler descriptors linked to messages.</description></item>
///   <item><description>Newly discovered messages awaiting finalization.</description></item>
///   <item><description>A lookup for processed types to avoid duplicate registrations.</description></item>
/// </list>
/// 
/// Generic message types are registered using their generic type definitions to ensure that different
/// constructed generic types (e.g., SomeMessage&lt;int&gt; and SomeMessage&lt;string&gt;) are treated
/// as a single generic definition.
/// 
/// This registry is designed for use in a messaging or CQRS framework to enable
/// efficient discovery, lookup, and management of message-handler relationships.
/// </remarks>
internal sealed class MessageRegistry(
    HandlerDescriptorBuilderFactory  handlerDescriptorBuilderFactory
    ) : IMessageRegistry
{
    
    /// <summary>
    /// All main handler descriptors
    /// </summary>
    private readonly List<IHandlerDescriptor> _descriptors = [];
    
    
    /// <summary>
    /// All messages (commands, queries, events, etc.)
    /// </summary>
    private readonly List<MessageDescriptor> _messages = [];
    
    
    /// <summary>
    /// Newly added, unprocessed messages
    /// These will be moved to <see cref="_messages"/> once processing is complete
    /// </summary>
    private readonly List<MessageDescriptor> _newMessages = [];
    
    /// <summary>
    /// Types that have already been processed and registered (as message or handler)
    /// </summary>
    private readonly ConcurrentDictionary<Type, byte> _processedTypes = new();
    
    
    /// <summary>
    /// Gets an enumerator that iterates through the collection of registered messages.
    /// </summary>
    /// <returns>An enumerator for the registered messages.</returns>
    /// <remarks>
    /// This allows enumeration over all registered messages.
    /// </remarks>
    public IEnumerator<IMessageDescriptor> GetEnumerator() => _messages.GetEnumerator();

    
    
    /// <summary>
    /// Gets the number of registered messages.
    /// </summary>
    /// <remarks>
    /// Represents how many unique message types are currently registered.
    /// </remarks>
    public int Count => _messages.Count;
    
    
    /// <summary>
    /// Gets an enumerator that iterates through the collection of registered messages (non-generic).
    /// </summary>
    /// <returns>An enumerator for the registered messages.</returns>
    /// <remarks>
    /// Non-generic enumerator required for IEnumerable implementation.
    /// </remarks>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    
    
    /// <summary>
    /// Registers a message or handler type into the registry.
    /// </summary>
    /// <param name="type">The message or handler type to register.</param>
    /// <remarks>
    /// If the type is a handler, its associated message types will also be registered.
    /// The method prevents duplicate registrations by tracking processed types.
    /// Handlers and messages are stored separately but linked via message descriptors.
    /// </remarks>
    public void Register(Type type)
    {
        
        // If the type has already been processed, skip registration
        if (_processedTypes.ContainsKey(type))
        {
            return;
        }
        
        // Use builders to create handler descriptors for the given type
        // <see cref="MessageDescriptorBuilderFactory" />

        var newDescriptors = handlerDescriptorBuilderFactory.BuildDescriptors(type);
       
        
        // If no handlers were created, assume the type is a message type and register it
        if (newDescriptors.Count == 0)
        {
            RegisterMessage(type);
        }
        
        else
        {
            // For each handler descriptor, register its message type and add the handler to the list
            foreach (var descriptor in newDescriptors)
            {
                RegisterMessage(descriptor.MessageType);
                _descriptors.Add(descriptor);
            }
            
            // If there are new handlers and existing messages, add the handlers to all messages
            if (newDescriptors.Count > 0 && _messages.Count > 0)
            {
                foreach (var messageDescriptor in _messages)
                {
                    messageDescriptor.AddDescriptors(newDescriptors);
                }
            }
        }
        
        // Sync new messages with all existing handler descriptors (if any)
        if (_newMessages.Count > 0 && _descriptors.Count > 0)
        {
            foreach (var messageDescriptor in _newMessages)
            {
                messageDescriptor.AddDescriptors(_descriptors);
            }
        }
        // Mark the type as processed to prevent duplicate registration
        _processedTypes[type] = 0;

        // Move all newly registered messages to the main messages list and clear the newMessages list
        if (_newMessages.Count > 0)
        {
            _messages.AddRange(_newMessages);
            _newMessages.Clear();
        }
    }
    
    
    
    
    /// <summary>
    /// Registers a message type if it is not already registered.
    /// </summary>
    /// <param name="messageType">The message type to register.</param>
    /// <remarks>
    /// Generic message types are registered by their generic type definition to avoid duplicate registrations of different constructed generic types.
    /// System namespace types and non-IMessage types are ignored or rejected.
    /// Newly registered messages are added to a temporary collection before being finalized.
    /// </remarks>
    private void RegisterMessage(Type messageType)
    {
        // Skip types from the System namespace to avoid registering framework/internal types
        if (messageType.Namespace is not null && messageType.Namespace.StartsWith("System"))
        {
            return;
        }

        // If the type is generic, register its generic definition instead (e.g. SomeMessage<>)
        // This prevents registering the same generic message multiple times for different type arguments
        messageType = messageType.IsGenericType ? messageType.GetGenericTypeDefinition() : messageType;

        // Check if the message type is already registered in the messages list
        var existingMessage = _messages.FirstOrDefault(
            d => d.MessageType == messageType
        );

        // _messages içinde aynı tip zaten varsa kayıt yapılmaz.
        if (existingMessage != null)
        {
            return;
        }

        // Also check if it's in the newMessages list awaiting processing
        var isNewMessage = _newMessages.FirstOrDefault(
            d => d.MessageType == messageType
        );

        if (isNewMessage != null) return;
    
        // Create a new MessageDescriptor and add it to the newMessages list
        _newMessages.Add(new MessageDescriptor(messageType));
    }
}
