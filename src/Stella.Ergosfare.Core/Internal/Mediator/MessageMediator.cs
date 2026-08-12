using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// Internal mediator responsible for dispatching messages to their corresponding handlers
/// and managing the execution context and dependencies for each message.
/// </summary>
/// <remarks>
/// The <see cref="MessageMediator"/> dispatches through the engine's cached pipeline
/// executors; the compiled composition decides what serves each message type.
/// </remarks>

internal sealed class MessageMediator(
    IMessageDependenciesFactory messageDependenciesFactory,
    IServiceProvider serviceProvider,
    PipelineExecutorCache? executorCache = null,
    MessageDispatchEngine? engine = null)
    : IMessageMediator
{
    /// <inheritdoc />
    public ValueTask DispatchAsync(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return RequireEngine().DispatchAsync(message, _serviceProvider, items, cancellationToken, groups);
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return RequireEngine().DispatchAsync<TResult>(message, _serviceProvider, items, cancellationToken, groups);
    }

    /// <inheritdoc />
    public ValueTask DispatchAsync(object message, ErgosfareContext context, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        return RequireEngine().DispatchAsync(message, context, _serviceProvider, groups);
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(object message, ErgosfareContext context, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        return RequireEngine().DispatchAsync<TResult>(message, context, _serviceProvider, groups);
    }

    /// <summary>
    /// The provider of the scope this mediator was resolved from — the broadcast fast lane
    /// (events assembly, via InternalsVisibleTo) dispatches strategies against it directly.
    /// </summary>
    internal IServiceProvider ScopeProvider => _serviceProvider;

    /// <summary>
    /// The dependencies factory backing this mediator; see <see cref="ScopeProvider"/>.
    /// </summary>
    internal IMessageDependenciesFactory DependenciesFactory => _messageDependenciesFactory;

    private MessageDispatchEngine RequireEngine()
        => _engine ?? throw new InvalidOperationException(
            "Executor dispatch requires the PipelineExecutorCache; register Ergosfare through AddErgosfare.");


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
    /// The dispatch engine the executor overloads delegate to. DI injects the container's
    /// singleton; directly-constructed mediators (tests) that supply only an executor cache
    /// get a private engine wrapping it, preserving the original optional-cache contract.
    /// Declared after the null-validated fields above so a null factory still fails their
    /// argument checks first.
    /// </summary>
    private readonly MessageDispatchEngine? _engine =
        engine ?? (executorCache is null ? null : new MessageDispatchEngine(executorCache, messageDependenciesFactory));
}
