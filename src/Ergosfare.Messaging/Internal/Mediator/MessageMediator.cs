using Ergosfare.Messaging.Abstractions;
using Ergosfare.Messaging.Abstractions.Context;
using Ergosfare.Messaging.Abstractions.Exceptions;
using Ergosfare.Messaging.Abstractions.Registry;
using ExecutionContext = Ergosfare.Messaging.Internal.Contexts.ExecutionContext;

namespace Ergosfare.Messaging.Internal.Mediator;

internal sealed class MessageMediator(
    IMessageRegistry messageRegistry,
    IServiceProvider serviceProvider)
    : IMessageMediator
{
    private readonly IMessageRegistry _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));


    public TResult Mediate<TMessage, TResult>(TMessage message, MediateOptions<TMessage, TResult> options) where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(options);
        // Create a new execution context for the current scope
        var executionContext = new ExecutionContext(options.CancellationToken,  options.Items);
        
        
        // Use a scope to manage the execution context
        using var _ = AmbientExecutionContext.CreateScope(executionContext);
        // Get the actual type of the message
        var messageType = message.GetType();
        
        var descriptor = options.MessageResolveStrategy.Find(messageType, _messageRegistry);
        
        
        if (descriptor is null)
        {
            if (!options.RegisterPlainMessagesOnSpot)
            {
                throw new NoHandlerFoundException(messageType);
            }

            _messageRegistry.Register(messageType);

            descriptor = options.MessageResolveStrategy.Find(messageType, _messageRegistry);
        }

        if (descriptor is null)
        {
            throw new InvalidOperationException($"No descriptor found for message type {messageType} with specified resolve strategy.");
        }

        // Resolve the dependencies in lazy mode
        var messageDependencies = new MessageDependencies(messageType, descriptor, _serviceProvider, []);

        // Mediate the message using the specified strategy
        return options.MessageMediationStrategy.Mediate(message, messageDependencies, AmbientExecutionContext.Current);
    }
}