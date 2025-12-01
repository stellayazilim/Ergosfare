using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Factories;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Internal.Contexts;

namespace Ergosfare.Core.Internal.Mediator;


/// <summary>
/// Internal mediator responsible for dispatching messages to their corresponding handlers
/// and managing the execution context and dependencies for each message.
/// </summary>
/// <remarks>
/// The <see cref="MessageMediator"/> uses the provided <see cref="IMessageRegistry"/> to track message types,
/// <see cref="ISignalHub"/> to publish events (if applicable), and <see cref="IMessageDependenciesFactory"/>
/// to lazily resolve handler dependencies. It ensures that the message mediation occurs
/// within a controlled execution context scope.
/// </remarks>
internal sealed class MessageMediator(
    IMessageRegistry messageRegistry,
    ISignalHub  signalHub,
    IMessageDependenciesFactory messageDependenciesFactory)
    : IMessageMediator
{
    
    /// <summary>
    /// Registry used to keep track of registered message types.
    /// </summary>
    private readonly IMessageRegistry _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));
    
    
    /// <summary>
    /// Factory used to create message handler dependencies for a given message type and descriptor.
    /// </summary>
    private readonly IMessageDependenciesFactory _messageDependenciesFactory = messageDependenciesFactory ?? throw new ArgumentNullException(nameof(messageDependenciesFactory));

    
    /// <summary>
    /// Dispatches a message of type <typeparamref name="TMessage"/> to its corresponding handler(s)
    /// and returns the result of type <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to mediate. Must be non-nullable.</typeparam>
    /// <typeparam name="TResult">The expected result type returned from the handler.</typeparam>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="options">Options that control how the message is resolved, dependencies are created, and mediation strategy applied.</param>
    /// <returns>The result produced by the handler after processing the message.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
    /// <exception cref="NoHandlerFoundException">Thrown if no handler descriptor is found for the message type and <c>RegisterPlainMessagesOnSpot</c> is false.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the message descriptor cannot be resolved with the specified resolve strategy.</exception>
    /// <remarks>
    /// The mediation process involves the following steps:
    /// <list type="number">
    /// <item>Create a new <see cref="ErgosfareExecutionContext"/> for the current message and options.</item>
    /// <item>Establish a scoped ambient execution context via <see cref="AmbientExecutionContext.CreateScope"/>.</item>
    /// <item>Resolve the message type and find the corresponding handler descriptor using the <see cref="MediateOptions{TMessage, TResult}.MessageResolveStrategy"/>.</item>
    /// <item>If no descriptor exists and <c>RegisterPlainMessagesOnSpot</c> is true, register the message type on-the-fly.</item>
    /// <item>Use the <see cref="IMessageDependenciesFactory"/> to create handler dependencies.</item>
    /// <item>Mediate the message via the <see cref="MediateOptions{TMessage, TResult}.MessageMediationStrategy"/> using the resolved dependencies and current execution context.</item>
    /// </list>
    /// </remarks>
    public TResult Mediate<TMessage, TResult>(TMessage message, MediateOptions<TMessage, TResult> options) where TMessage : notnull
    {
        
    
        ArgumentNullException.ThrowIfNull(options);
        

        // Use a scope to manage the execution context
        using var _ = AmbientExecutionContext.CreateScope(new ErgosfareExecutionContext( options.Items, options.CancellationToken));
        // Get the actual type of the message
        var messageType = message.GetType();
        
        var descriptor = options.MessageResolveStrategy.Find(messageType);
        
        
        if (descriptor is null)
        {
            if (!options.RegisterPlainMessagesOnSpot)
            {
                throw new NoHandlerFoundException(messageType);
            }

            _messageRegistry.Register(messageType);

            descriptor = options.MessageResolveStrategy.Find(messageType);
        }

        if (descriptor is null)
        {
            throw new InvalidOperationException($"No descriptor found for message type {messageType} with specified resolve strategy.");
        }

        // Resolve the dependencies in lazy mode
        var messageDependencies = _messageDependenciesFactory.Create(messageType, descriptor, options.Groups);

        // Mediate the message using the specified strategy
        // natural pipeline execution with fresh context
        return options.MessageMediationStrategy.Mediate(message, messageDependencies,
            AmbientExecutionContext.Current);
    }
}