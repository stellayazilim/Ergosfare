// The executor ends a stream message's channel when its pipeline stops, which is what the
// experimental streaming surface exists for.
#pragma warning disable ERGOEXP003

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
/// <param name="dependenciesFactory">The factory participants are verified through.</param>
/// <param name="directHandlerFactory">
/// Constructs the handler without the container, when the plan carries a way to.
/// </param>
/// <param name="providerHandlerFactory">
/// Constructs the handler with its dependencies resolved from a provider, when the plan
/// carries a way to.
/// </param>
/// <remarks>
/// The first dispatch verifies the plan exactly as on the void side, and a pipeline that is
/// not the plan's fails the dispatch naming what diverged. What that dispatch settles is
/// kept.
/// </remarks>
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

    // The adapter bound to this pipeline's result type, resolved on the first dispatch. A
    // single-handler plan assumes none, so one being bound fails the dispatch.
    private IResultAdapter<TResult>? _resultAdapter;
    private volatile bool _resultAdapterResolved;

    // A disposable handler is never constructed here; see the void executor.
    private static readonly bool HandlerIsDisposable =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler));

    private readonly Func<THandler>? _directHandlerFactory = HandlerIsDisposable ? null : directHandlerFactory;
    private readonly Func<IServiceProvider, THandler>? _providerHandlerFactory = HandlerIsDisposable ? null : providerHandlerFactory;

    /// <inheritdoc cref="GeneratedVoidPipelineExecutor{TMessage, THandler}._handlerReference"/>
    private IHandlerReference<IHandler>? _handlerReference;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}._verdict"/>
    private volatile int _verdict;

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.Undecided"/>
    // ReSharper disable once UnusedMember.Local
    private const int Undecided = 0;

    /// <summary>
    /// Resolve the planned handler from the container and call it.
    /// </summary>
    private const int Resolve = 2;

    /// <summary>
    /// Construct the planned handler directly and call it.
    /// </summary>
    private const int FastDirect = 3;

    /// <inheritdoc />
    public ValueTask<TResult> Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        // A message that carries chunks has a second half the pipeline has to close. One type
        // test on the ordinary path; the wrapper exists only where there is a stream.
        if (message is global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream)
        {
            return ExecuteAndEndStream(stream, message, context, serviceProvider, groups);
        }

        return ExecuteCore(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Runs the pipeline and ends the message's stream however it turns out.
    /// </summary>
    /// <param name="stream">The message's chunk-carrying half.</param>
    /// <param name="message">The message to run.</param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <remarks>
    /// A pipeline that stops has to end the stream with it. The two are separate
    /// synchronisation objects, so a caller waiting on a full buffer learns nothing from the
    /// dispatch failing — it would wait for a reader that is never coming.
    /// </remarks>
    private async ValueTask<TResult> ExecuteAndEndStream(global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream, object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups)
    {
        try
        {
            return await ExecuteCore(message, context, serviceProvider, groups).ConfigureAwait(false);
        }
        finally
        {
            stream.EndDispatch();
        }
    }

    /// <summary>
    /// The pipeline itself, for a message with chunks or without.
    /// </summary>
    /// <param name="message">The message to run.</param>
    /// <param name="context">The execution context for this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteCore(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups)
    {
        if (groups is not null)
        {
            return ExecuteGrouped(message, context, serviceProvider, groups);
        }

        var verdict = _verdict;

        if (verdict == FastDirect)
        {
            var direct = _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider);
            return direct.HandleAsync((TMessage)message, context);
        }

        if (verdict == Resolve)
        {
            return CallPlanned(_handlerReference!, message, context, serviceProvider);
        }

        return ExecuteUndecided(message, context, serviceProvider);
    }

    /// <summary>
    /// Runs the first dispatch and settles the route the rest take; see
    /// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}.ExecuteUndecided"/>.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>The result the pipeline produced.</returns>
    private ValueTask<TResult> ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
    {
        if (dependenciesFactory is not MessageDependenciesFactory typedFactory)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TMessage));
        }

        EnsureResultAdapter(serviceProvider);

        // Throws for a message no composition serves — and on every dispatch of such a
        // message, since nothing is settled until the plan is verified.
        if (typedFactory.Create(typeof(TMessage), []) is not MessageDependencies dependencies)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TMessage));
        }

        var handlerReference = VerifyPlannedHandler(dependencies);

        var useDirectConstruction = (_directHandlerFactory is not null || _providerHandlerFactory is not null)
            && typedFactory.IsPlainTransientRegistration(typeof(THandler));

        _handlerReference = handlerReference;
        _verdict = useDirectConstruction ? FastDirect : Resolve;

        if (useDirectConstruction)
        {
            var direct = _directHandlerFactory is not null ? _directHandlerFactory() : _providerHandlerFactory!(serviceProvider);
            return direct.HandleAsync((TMessage)message, context);
        }

        return CallPlanned(handlerReference, message, context, serviceProvider);
    }

    /// <summary>
    /// Runs a dispatch that named groups; see
    /// <see cref="GeneratedVoidPipelineExecutor{TMessage, THandler}.ExecuteGrouped"/>.
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

        if (composition.Fast is not { } fast)
        {
            throw UnplannedDispatch.ForForeignFactory(typeof(TMessage));
        }

        var handlerReference = VerifyPlannedHandler(fast);

        return CallPlanned(handlerReference, message, context, serviceProvider);
    }

    /// <inheritdoc cref="GeneratedVoidPipelineExecutor{TMessage, THandler}.VerifyPlannedHandler"/>
    private IHandlerReference<IHandler> VerifyPlannedHandler(MessageDependencies dependencies)
    {
        if (_resultAdapter is not null)
        {
            throw UnplannedDispatch.ForResultAdapterMismatch(
                typeof(TMessage), compiledAdapterType: null, _resultAdapter.GetType());
        }

        if (dependencies.ForcedMemoization)
        {
            throw UnplannedDispatch.ForMemoizedInstances(typeof(TMessage));
        }

        if (dependencies.FastSingleHandler is not { } handlerReference
            || handlerReference.HandlerType != typeof(THandler))
        {
            throw UnplannedDispatch.ForDivergedHandlerPlan(typeof(TMessage), typeof(THandler), dependencies);
        }

        return handlerReference;
    }

    /// <inheritdoc cref="GeneratedVoidPipelineExecutor{TMessage, THandler}.CallPlanned"/>
    private static ValueTask<TResult> CallPlanned(
        IHandlerReference<IHandler> handlerReference, object message, ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        if (handlerReference.Resolve(serviceProvider) is not THandler planned)
        {
            // The container binds the planned type to something else entirely; the pipeline
            // in hand is not the compiled one.
            throw UnplannedDispatch.ForDivergedHandlerRegistration(typeof(TMessage), typeof(THandler));
        }

        return planned.HandleAsync((TMessage)message, context);
    }

    /// <inheritdoc cref="FrozenVoidDispatch{TMessage}.EnsureResultAdapter"/>
    private void EnsureResultAdapter(IServiceProvider serviceProvider)
    {
        if (_resultAdapterResolved)
        {
            return;
        }

        _resultAdapter = ResultAdapterBinding.For<TMessage, TResult>(serviceProvider);
        _resultAdapterResolved = true;
    }
}
