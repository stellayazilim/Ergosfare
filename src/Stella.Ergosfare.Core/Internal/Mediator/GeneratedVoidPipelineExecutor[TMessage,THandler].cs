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
/// The void pipeline a single-handler plan produces: closed over the message and the one
/// handler the plan named, so the handler is called through its own type rather than found
/// by testing contracts.
/// </summary>
/// <typeparam name="TMessage">The message type this pipeline serves.</typeparam>
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
/// The first dispatch verifies the plan against the participants this container actually
/// resolved. A pipeline that is not the plan's — another handler, an interceptor, a bound
/// adapter, memoized instances — fails the dispatch naming what diverged; it does not run
/// through any other lane. What the first dispatch settles is kept; a registration made
/// afterwards is not noticed.
/// </remarks>
internal sealed class GeneratedVoidPipelineExecutor<TMessage, THandler>(
    IMessageDependenciesFactory dependenciesFactory,
    Func<THandler>? directHandlerFactory = null,
    Func<IServiceProvider, THandler>? providerHandlerFactory = null) : IPipelineExecutor
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage>
{

    /// <summary>
    /// This pipeline's participants per group set. The plan describes the unfiltered
    /// pipeline only, so a filtered dispatch verifies that the set still selects exactly
    /// the planned handler.
    /// </summary>
    private readonly GroupedCompositions _grouped = new(dependenciesFactory, typeof(TMessage));

    // The adapter bound to this pipeline's Unit slot, resolved on the first dispatch. A
    // single-handler plan assumes none, so one being bound fails the dispatch.
    private IResultAdapter<Unit>? _resultAdapter;
    private volatile bool _resultAdapterResolved;

    // A disposable handler is never constructed here: the container tracks transient
    // disposables in the scope that resolved them, and constructing one directly would
    // leave nothing to dispose it. The provider-taking form covers handlers with
    // constructor dependencies, resolving those from the dispatching scope exactly as
    // container activation would.
    private static readonly bool HandlerIsDisposable =
        typeof(IDisposable).IsAssignableFrom(typeof(THandler)) || typeof(IAsyncDisposable).IsAssignableFrom(typeof(THandler));

    private readonly Func<THandler>? _directHandlerFactory = HandlerIsDisposable ? null : directHandlerFactory;
    private readonly Func<IServiceProvider, THandler>? _providerHandlerFactory = HandlerIsDisposable ? null : providerHandlerFactory;

    /// <summary>
    /// The planned handler's reference in this container, kept once the plan is verified so
    /// the resolving route reads a field.
    /// </summary>
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
    public ValueTask Execute(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <remarks>
    /// A pipeline that stops has to end the stream with it. The two are separate
    /// synchronisation objects, so a caller waiting on a full buffer learns nothing from the
    /// dispatch failing — it would wait for a reader that is never coming.
    /// </remarks>
    private async ValueTask ExecuteAndEndStream(global::Stella.Ergosfare.Core.Abstractions.Streaming.ErgosfareStream stream, object message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups)
    {
        try
        {
            await ExecuteCore(message, context, serviceProvider, groups).ConfigureAwait(false);
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
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteCore(object message, ErgosfareContext context, IServiceProvider serviceProvider,
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
    /// Runs the first dispatch: binds the adapter, resolves the participants, verifies the
    /// plan against them, then calls the planned handler — or fails the dispatch naming
    /// what could not be verified.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteUndecided(object message, ErgosfareContext context, IServiceProvider serviceProvider)
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
    /// Runs a dispatch that named groups, verifying that the set still selects exactly the
    /// planned handler.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    private ValueTask ExecuteGrouped(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string> groups)
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

    /// <summary>
    /// Verifies that one live composition is exactly the plan's — the planned handler
    /// alone, no interceptors, no adapter, no memoization — and hands back its reference.
    /// </summary>
    /// <param name="dependencies">The live participants.</param>
    /// <returns>The planned handler's reference.</returns>
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

    /// <summary>
    /// Resolves the planned handler through its reference and calls it.
    /// </summary>
    /// <param name="handlerReference">The planned handler's reference.</param>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider the handler is resolved from.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    /// <remarks>
    /// Abort handling is absent on purpose — the engine's frame owns it, and an
    /// exception-handling region here would stop this method being inlined into its caller
    /// on every dispatch.
    /// </remarks>
    private static ValueTask CallPlanned(
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

        _resultAdapter = ResultAdapterBinding.For<TMessage, Unit>(serviceProvider);
        _resultAdapterResolved = true;
    }
}
