using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The result-producing pipeline a single-handler plan produces; the counterpart of
/// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>, closed over the message,
/// its result and the one handler the plan named.
/// </summary>
/// <typeparam name="TMessage">The message type this pipeline serves.</typeparam>
/// <typeparam name="TResult">The result type it produces.</typeparam>
/// <typeparam name="THandler">The handler the plan named.</typeparam>
/// <param name="dependenciesFactory">The factory participants are resolved through.</param>
/// <param name="directHandlerFactory">
/// Constructs the handler without the container, when the plan carries a way to.
/// </param>
/// <param name="providerHandlerFactory">
/// Constructs the handler with its dependencies resolved from a provider, when the plan
/// carries a way to.
/// </param>
/// <remarks>
/// The plan is checked on the first dispatch and anything unexpected falls back to the
/// general pipeline, exactly as on the void side; what that dispatch settles is kept.
/// </remarks>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class GeneratedResultPipelineExecutor<TMessage, TResult, THandler>(
    IMessageDependenciesFactory dependenciesFactory,
    Func<THandler>? directHandlerFactory = null,
    Func<IServiceProvider, THandler>? providerHandlerFactory = null) : IPipelineExecutor<TResult>
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{

    /// <summary>
    /// This pipeline's participants per group set; see
    /// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>.
    /// </summary>
    private readonly GroupedCompositions _grouped = new(dependenciesFactory, typeof(TMessage));

    // The adapter bound to this pipeline's result type, resolved on the first dispatch so
    // the routes below cost nothing when there is none, which is nearly always.
    private IResultAdapter<TResult>? _resultAdapter;
    private IResultMaterializer<TResult>? _resultMaterializer;
    private volatile bool _resultAdapterResolved;

    // A disposable handler is never constructed here; see the void executor.
    private static readonly bool HandlerIsDisposable =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler));

    private readonly Func<THandler>? _directHandlerFactory = HandlerIsDisposable ? null : directHandlerFactory;
    private readonly Func<IServiceProvider, THandler>? _providerHandlerFactory = HandlerIsDisposable ? null : providerHandlerFactory;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;

    // Whether the handler may be constructed here instead of resolved; see the void
    // executor for the conditions.
    private bool _useDirectConstruction;

    // Whether the whole short route is available — planned handler, direct construction, no
    // adapter — settled by the first dispatch. From then on the handler is constructed and
    // called without the participants being consulted at all.
    private bool _fastDirect;

    /// <inheritdoc />
    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return ExecuteGrouped(message, context, serviceProvider, groups);
        }

        if (_fastDirect)
        {
            var direct = _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider);
            return direct.HandleAsync((TMessage)message, context);
        }

        EnsureResultAdapter(serviceProvider);

        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && _resultAdapter is null)
        {
            // Re-checking the handler type ties the decision to the reference actually in
            // hand, so this can only fall back to the container, never construct a type the
            // pipeline no longer names.
            IHandler handler = _useDirectConstruction && handlerReference.HandlerType == typeof(THandler)
                ? _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider)
                : handlerReference.Resolve(serviceProvider);

            // Abort handling is absent on purpose — the engine's frame owns it, and an
            // exception-handling region here would stop this method being inlined into its
            // caller on every dispatch.
            if (handler is THandler planned)
            {
                return planned.HandleAsync((TMessage)message, context);
            }

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }
        }

        return ResultPipelineBody<TMessage, TResult>.Run(
            (TMessage)message, dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <summary>
    /// Runs a dispatch that named groups; see
    /// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteGrouped(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string> groups)
    {
        EnsureResultAdapter(serviceProvider);

        var composition = _grouped.Resolve(groups);

        if (composition.Fast?.FastSingleHandler is { } handlerReference && _resultAdapter is null)
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage, TResult> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask<TResult>> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, TResult> syncHandler:
                    return ValueTask.FromResult(syncHandler.Handle((TMessage)message, context));
            }
        }

        return ResultPipelineBody<TMessage, TResult>.Run(
            (TMessage)message, composition.Dependencies, _resultAdapter, _resultMaterializer, context, serviceProvider);
    }

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.EnsureResultAdapter"/>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        var adapter = ResultAdapterBinding.For<TMessage, TResult>(serviceProvider);
        _resultAdapter = adapter;
        _resultMaterializer = adapter as IResultMaterializer<TResult>;
        _resultAdapterResolved = true;
    }

    /// <summary>
    /// Returns this pipeline's participants, resolving and settling the plan's conditions on
    /// the first call.
    /// </summary>
    /// <returns>The participants for the ungrouped pipeline.</returns>
    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            // Resolved once and kept: a registration made after the first dispatch is not
            // noticed.
            var cached = _cachedDependencies;

            if (cached is not null)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), []);
            var fastDependencies = dependencies as MessageDependencies;
            _cachedFastDependencies = fastDependencies;
            _cachedDependencies = dependencies;
            _useDirectConstruction = (_directHandlerFactory is not null || _providerHandlerFactory is not null)
                && fastDependencies is { MemoizedInstances: false, FastSingleHandler.HandlerType: var plannedType }
                && plannedType == typeof(THandler)
                && typedFactory.IsPlainTransientRegistration(typeof(THandler));
            // Execute binds the adapter before it first calls this, so that answer is
            // already available here.
            _fastDirect = _useDirectConstruction && _resultAdapter is null;
            return dependencies;
        }

        _useDirectConstruction = false;
        return dependenciesFactory.Create(typeof(TMessage), []);
    }
}
#pragma warning restore CS8714
