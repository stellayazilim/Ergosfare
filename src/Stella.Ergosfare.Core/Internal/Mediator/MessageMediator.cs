using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Core.Internal.Mediator;


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
    IMessageDependenciesFactory messageDependenciesFactory,
    IServiceProvider serviceProvider,
    PipelineExecutorCache? executorCache = null)
    : IMessageMediator
{
    /// <summary>
    /// Process-wide executor cache used by the <see cref="DispatchAsync(object, IDictionary{object, object?}?, CancellationToken)"/>
    /// path. Optional so directly-constructed mediators (tests) keep working; the DI
    /// registration always supplies it.
    /// </summary>
    private readonly PipelineExecutorCache? _executorCache = executorCache;

    /// <inheritdoc />
    public ValueTask DispatchAsync(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = RequireExecutorCache().GetVoidExecutor(message.GetType(), groups);
        var context = new ErgosfareExecutionContext(items, cancellationToken);

        return executor.Execute(message, context, _serviceProvider);
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = RequireExecutorCache().GetExecutor<TResult>(message.GetType(), groups);
        var context = new ErgosfareExecutionContext(items, cancellationToken);

        return executor.Execute(message, context, _serviceProvider);
    }

    private PipelineExecutorCache RequireExecutorCache()
        => _executorCache ?? throw new InvalidOperationException(
            "Executor dispatch requires the PipelineExecutorCache; register Ergosfare through AddErgosfare or use Mediate with explicit options.");


    /// <summary>
    /// Registry used to keep track of registered message types.
    /// </summary>
    private readonly IMessageRegistry _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));


    /// <summary>
    /// Factory used to create message handler dependencies for a given message type and descriptor.
    /// </summary>
    private readonly IMessageDependenciesFactory _messageDependenciesFactory = messageDependenciesFactory ?? throw new ArgumentNullException(nameof(messageDependenciesFactory));

    /// <summary>
    /// The provider of the scope this mediator was resolved from; passed to the mediation
    /// strategy on each dispatch so handlers resolve against the calling scope.
    /// </summary>
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    
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
    /// <item>Resolve the message type and find the corresponding handler descriptor using the <see cref="MediateOptions{TMessage, TResult}.MessageResolveStrategy"/>.</item>
    /// <item>If no descriptor exists and <c>RegisterPlainMessagesOnSpot</c> is true, register the message type on-the-fly.</item>
    /// <item>Use the <see cref="IMessageDependenciesFactory"/> to create handler dependencies.</item>
    /// <item>Mediate the message via the <see cref="MediateOptions{TMessage, TResult}.MessageMediationStrategy"/> using the resolved dependencies and current execution context.</item>
    /// </list>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "RegisterPlainMessagesOnSpot registers the runtime type of a live message instance; " +
                        "the instance roots its type, and this opt-in dynamic path is not supported on trimmed apps — " +
                        "pre-register plain messages explicitly there.")]
    public TResult Mediate<TMessage, TResult>(TMessage message, MediateOptions<TMessage, TResult> options) where TMessage : notnull
    {


        ArgumentNullException.ThrowIfNull(options);

        var context = new ErgosfareExecutionContext(options.Items, options.CancellationToken);

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

        // Mediate the message using the specified strategy. The scope's provider is
        // handed to the strategy explicitly — handler resolution belongs to the
        // dispatch pipeline, never to the execution context.
        return options.MessageMediationStrategy.Mediate(message, messageDependencies, context, _serviceProvider);
    }
}