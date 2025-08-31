using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Factories;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Internal.Contexts;

namespace Ergosfare.Core.Internal.Mediator;

internal sealed class MessageMediator(
    IMessageRegistry messageRegistry,
    IMessageDependenciesFactory messageDependenciesFactory)
    : IMessageMediator
{
    private readonly IMessageRegistry _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));
    private readonly IMessageDependenciesFactory _messageDependenciesFactory = messageDependenciesFactory ?? throw new ArgumentNullException(nameof(messageDependenciesFactory));

    public TResult Mediate<TMessage, TResult>(TMessage message, MediateOptions<TMessage, TResult> options) where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        // Create a new execution context for the current scope
        var executionContext = new ErgosfareExecutionContext(options.Items, options.CancellationToken);
        
        
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
        var messageDependencies = _messageDependenciesFactory.Create(messageType, descriptor, options.Groups);

        // Mediate the message using the specified strategy
        return  options.MessageMediationStrategy.Mediate(message, messageDependencies, AmbientExecutionContext.Current);
    }
}