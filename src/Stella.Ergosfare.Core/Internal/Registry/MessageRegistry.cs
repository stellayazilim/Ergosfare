using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;


namespace Stella.Ergosfare.Core.Internal.Registry;



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
    /// Index of every registered message descriptor (finalized or staged) by message type.
    /// Registration-time lookups are O(1) instead of scanning <see cref="_messages"/> and
    /// <see cref="_newMessages"/>.
    /// </summary>
    private readonly Dictionary<Type, MessageDescriptor> _messageIndex = new();

    /// <summary>
    /// Types that have already been processed and registered (as message or handler).
    /// Concurrent so the lock-free fast path of <see cref="Register"/> can consult it;
    /// authoritative writes happen under <see cref="_gate"/>.
    /// </summary>
    private readonly ConcurrentDictionary<Type, byte> _processedTypes = new();

    /// <summary>
    /// Serializes all registration work. Dispatch-time readers never take this lock —
    /// they enumerate <see cref="_snapshot"/>, an immutable array republished after every
    /// effective registration.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// Immutable view of the finalized messages, republished at the end of every effective
    /// registration. The volatile write is the publication point: everything a registration
    /// mutated (descriptor stage arrays included) happens-before a reader that observes the
    /// new snapshot or the new <see cref="Version"/>. Concrete element type on purpose —
    /// the array is only ever reassigned wholesale, and the interface view readers get goes
    /// through <see cref="IEnumerable{T}"/> covariance, not array covariance.
    /// </summary>
    private volatile MessageDescriptor[] _snapshot = [];


    /// <summary>
    /// Monotonically increasing version, bumped on every effective registration.
    /// Unlike <see cref="Count"/>, this also changes when handlers are added to
    /// already-registered messages, allowing dependency caches to invalidate.
    /// </summary>
    internal int Version => Volatile.Read(ref _version);

    private int _version;


    /// <summary>
    /// Gets an enumerator that iterates through the collection of registered messages.
    /// </summary>
    /// <returns>An enumerator for the registered messages.</returns>
    /// <remarks>
    /// Enumerates an immutable snapshot, so enumeration is safe while another thread
    /// registers; a concurrent registration becomes visible on the next enumeration.
    /// </remarks>
    public IEnumerator<IMessageDescriptor> GetEnumerator() => ((IEnumerable<IMessageDescriptor>)_snapshot).GetEnumerator();



    /// <summary>
    /// Gets the number of registered messages.
    /// </summary>
    /// <remarks>
    /// Represents how many unique message types are currently registered.
    /// </remarks>
    public int Count => _snapshot.Length;
    
    
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
    public void Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {

        // Lock-free fast path: already-processed types (every dispatch-time re-registration
        // attempt after the first) never touch the gate.
        if (_processedTypes.ContainsKey(type))
        {
            return;
        }

        lock (_gate)
        {
            // Re-check under the lock: another thread may have registered the same type
            // between the fast-path check and lock acquisition.
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

            // Move all newly registered messages to the main messages list and clear the newMessages list
            if (_newMessages.Count > 0)
            {
                _messages.AddRange(_newMessages);
                _newMessages.Clear();
            }

            Publish();

            // Mark processed only after publishing: a concurrent caller that observes the
            // fast-path hit must also observe the snapshot containing this registration.
            _processedTypes[type] = 0;
        }
    }

    /// <summary>
    /// Publishes the effects of a registration to lock-free readers: bumps the version
    /// (dependency caches invalidate on it) and republishes the immutable message snapshot.
    /// Must be called under <see cref="_gate"/>, after all mutations of the registration.
    /// </summary>
    private void Publish()
    {
        Interlocked.Increment(ref _version);
        _snapshot = _messages.ToArray();
    }


    /// <summary>
    /// Registers pre-built handler descriptors, bypassing the reflection-based descriptor
    /// builders. This is the ahead-of-time registration seam used by source-generated code.
    /// </summary>
    /// <param name="descriptors">The handler descriptors to register.</param>
    /// <remarks>
    /// Handler types already registered — through either <see cref="Register"/> or a prior
    /// injection — are skipped, so generated registration and the runtime scanning fallback
    /// can coexist without producing duplicate descriptors.
    /// </remarks>
    public void RegisterDescriptors(IEnumerable<IHandlerDescriptor> descriptors)
    {
        lock (_gate)
        {
            // Collect first, mark processed after: one handler type may legitimately supply
            // several descriptors in the same batch (it can implement multiple handler roles).
            List<IHandlerDescriptor>? accepted = null;
            foreach (var descriptor in descriptors)
            {
                if (_processedTypes.ContainsKey(descriptor.HandlerType))
                {
                    continue;
                }

                (accepted ??= []).Add(descriptor);
            }

            if (accepted is null)
            {
                return;
            }

            foreach (var descriptor in accepted)
            {
                RegisterMessage(descriptor.MessageType);
                _descriptors.Add(descriptor);
            }

            // Same linking pass as Register(): attach the new descriptors to every existing
            // message, then give staged messages the full descriptor set before finalizing.
            if (_messages.Count > 0)
            {
                foreach (var messageDescriptor in _messages)
                {
                    messageDescriptor.AddDescriptors(accepted);
                }
            }

            if (_newMessages.Count > 0 && _descriptors.Count > 0)
            {
                foreach (var messageDescriptor in _newMessages)
                {
                    messageDescriptor.AddDescriptors(_descriptors);
                }
            }

            if (_newMessages.Count > 0)
            {
                _messages.AddRange(_newMessages);
                _newMessages.Clear();
            }

            Publish();

            // Mark processed only after publishing: a concurrent Register() caller that
            // observes the fast-path hit must also observe the published snapshot.
            foreach (var descriptor in accepted)
            {
                _processedTypes[descriptor.HandlerType] = 0;
            }
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

        // The index covers both finalized and staged messages, so a single O(1) lookup
        // replaces the previous linear scans over _messages and _newMessages.
        if (_messageIndex.ContainsKey(messageType))
        {
            return;
        }

        // Create a new MessageDescriptor and stage it in the newMessages list
        var descriptor = new MessageDescriptor(messageType);
        _messageIndex[messageType] = descriptor;
        _newMessages.Add(descriptor);
    }
}
