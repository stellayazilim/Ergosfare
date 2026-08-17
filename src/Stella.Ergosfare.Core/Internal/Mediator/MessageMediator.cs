using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// The <see cref="IMessageMediator"/> a scope resolves: it holds that scope's provider and
/// forwards every dispatch to the container's <see cref="MessageDispatchEngine"/>.
/// </summary>
/// <param name="messageDependenciesFactory">The container's dependencies factory.</param>
/// <param name="serviceProvider">The provider of the scope this mediator was resolved from.</param>
/// <param name="executorCache">
/// The container's pipeline executors. Supplying this without an engine builds a private
/// engine over it.
/// </param>
/// <param name="engine">The container's dispatch engine.</param>
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

        return RequireEngine().DispatchAsync(message, _serviceProvider, cancellationToken, groups);
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return RequireEngine().DispatchAsync<TResult>(message, _serviceProvider, cancellationToken, groups);
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
    /// The provider of the scope this mediator was resolved from. The events assembly
    /// dispatches against it directly on its publish path.
    /// </summary>
    internal IServiceProvider ScopeProvider => _serviceProvider;

    /// <summary>
    /// The container's dependencies factory; exposed alongside <see cref="ScopeProvider"/>
    /// for the same reason.
    /// </summary>
    internal IMessageDependenciesFactory DependenciesFactory => _messageDependenciesFactory;

    /// <summary>
    /// Returns the engine, or explains that the mediator was built without one.
    /// </summary>
    /// <returns>The engine this mediator dispatches through.</returns>
    /// <exception cref="InvalidOperationException">
    /// The mediator was constructed with neither an engine nor an executor cache.
    /// </exception>
    private MessageDispatchEngine RequireEngine()
        => _engine ?? throw new InvalidOperationException(
            "Executor dispatch requires the PipelineExecutorCache; register Ergosfare through AddErgosfare.");


    /// <summary>
    /// The factory the engine builds its pipelines through.
    /// </summary>
    private readonly IMessageDependenciesFactory _messageDependenciesFactory = messageDependenciesFactory ?? throw new ArgumentNullException(nameof(messageDependenciesFactory));

    /// <summary>
    /// The provider of the scope this mediator was resolved from, passed to the engine on
    /// every dispatch so participants resolve against the calling scope.
    /// </summary>
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// The engine every dispatch goes through. Dependency injection supplies the
    /// container's; a mediator constructed by hand with only an executor cache gets a
    /// private engine wrapping it.
    /// </summary>
    /// <remarks>
    /// Declared after the null-checked fields above so that a <c>null</c> factory still
    /// fails their argument checks first.
    /// </remarks>
    private readonly MessageDispatchEngine? _engine =
        engine ?? (executorCache is null ? null : new MessageDispatchEngine(executorCache, messageDependenciesFactory));
}
