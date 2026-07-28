using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing pipeline closed over the concrete <typeparamref name="TMessage"/>.
/// Because <typeparamref name="TMessage"/> is the message's runtime type here, the mediation
/// strategy's typed seam always hits — the handler is invoked through its typed member and
/// its <see cref="ValueTask{TResult}"/> never crosses an object-typed bridge.
/// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal sealed class ResultPipelineExecutor<TMessage, TResult>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups) : IPipelineExecutor<TResult>
    where TMessage : notnull
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new(resultAdapterService);

    // Adapter service split by shape: the concrete service can report emptiness cheaply,
    // a foreign implementation always routes through the strategy (which consults it).
    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    // Dependencies cached per executor (executors are already per message type + groups),
    // re-validated against the registry version — turns the per-dispatch factory call and
    // cache lookup into a single field read + version compare.
    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;

    public ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        // Zero-interceptor, single-handler, no-adapter dispatch: invoke the handler's typed
        // member directly and hand its ValueTask straight back — no async state machine,
        // no interface-dispatched Count checks. Mirrors the strategy's fast path exactly.
        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
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

            // Unsupported handler contract: fall through so the strategy raises its
            // canonical NotSupportedException.
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            // Create runs the registry-version invalidation and rebuilds; races are benign —
            // both writers publish equivalent, idempotent state.
            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        // Foreign factory implementations keep the original per-dispatch behavior.
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}

#pragma warning restore CS8714

/// <summary>
/// Void pipeline closed over the concrete <typeparamref name="TMessage"/>; see
/// <see cref="ResultPipelineExecutor{TMessage, TResult}"/>.
/// </summary>
internal sealed class VoidPipelineExecutor<TMessage>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups) : IPipelineExecutor
    where TMessage : IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    private IMessageDependencies? _cachedDependencies;
    private MessageDependencies? _cachedFastDependencies;
    private int _cachedVersion = int.MinValue;

    public ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_cachedFastDependencies?.FastSingleHandler is { } handlerReference
            && !_foreignAdapters
            && (_concreteAdapters is null || _concreteAdapters.IsEmpty))
        {
            var handler = handlerReference.Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    return asyncHandler.HandleAsync((TMessage)message, context);
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    return valueTaskShaped.Handle((TMessage)message, context);
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle((TMessage)message, context);
                    return ValueTask.CompletedTask;
            }
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedFastDependencies = dependencies as MessageDependencies;
            _cachedDependencies = dependencies;
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}

/// <summary>
/// Process-wide cache of pipeline executors, one per (message runtime type, result type,
/// group set). Executor construction closes the generic executor over the message's runtime
/// type — one <see cref="Type.MakeGenericType"/> per message type, consistent with the
/// pipeline plan premise that all dispatch-shape work happens once per message type.
/// </summary>
internal sealed class PipelineExecutorCache(
    IMessageDependenciesFactory dependenciesFactory,
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService = null)
{
    internal static readonly string[] EmptyGroups = [];

    private readonly ConcurrentDictionary<(Type MessageType, string GroupsKey), IPipelineExecutor> _voidExecutors = new();
    private readonly ConcurrentDictionary<(Type MessageType, Type ResultType, string GroupsKey), object> _resultExecutors = new();

    // Group-less dispatch (the overwhelmingly common case) is keyed by message type alone:
    // no group materialization, no composite-key hashing on the hot path.
    private readonly ConcurrentDictionary<Type, IPipelineExecutor> _voidExecutorsByType = new();
    private readonly ConcurrentDictionary<(Type MessageType, Type ResultType), object> _resultExecutorsByType = new();

    public IPipelineExecutor GetVoidExecutor(Type messageType, IEnumerable<string>? groups = null)
    {
        if (groups is null)
        {
            if (_voidExecutorsByType.TryGetValue(messageType, out var fast))
            {
                return fast;
            }

            return _voidExecutorsByType.GetOrAdd(messageType,
                static (t, cache) => cache.CreateVoidExecutor(t, EmptyGroups), this);
        }

        var materializedGroups = MaterializeGroups(groups);
        var key = (messageType, GroupsKey(materializedGroups));

        if (!_voidExecutors.TryGetValue(key, out var executor))
        {
            executor = _voidExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateVoidExecutor(k.MessageType, state.Groups),
                (Cache: this, Groups: materializedGroups));
        }

        return executor;
    }

    public IPipelineExecutor<TResult> GetExecutor<TResult>(Type messageType, IEnumerable<string>? groups = null)
    {
        if (groups is null)
        {
            if (_resultExecutorsByType.TryGetValue((messageType, typeof(TResult)), out var fast))
            {
                // Entries are only ever created as IPipelineExecutor<TResult> for their
                // (message, result) key, so the interface cast can skip the runtime
                // covariance check — a measurable cost on the hot path.
                return Unsafe.As<IPipelineExecutor<TResult>>(fast);
            }

            return (IPipelineExecutor<TResult>)_resultExecutorsByType.GetOrAdd((messageType, typeof(TResult)),
                static (k, cache) => cache.CreateResultExecutor(k.MessageType, k.ResultType, EmptyGroups), this);
        }

        var materializedGroups = MaterializeGroups(groups);
        var key = (messageType, typeof(TResult), GroupsKey(materializedGroups));

        if (!_resultExecutors.TryGetValue(key, out var executor))
        {
            executor = _resultExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateResultExecutor(k.MessageType, k.ResultType, state.Groups),
                (Cache: this, Groups: materializedGroups));
        }

        return (IPipelineExecutor<TResult>)executor;
    }

    private static string GroupsKey(string[] groups)
        => groups.Length == 0 ? string.Empty : string.Join('\x1f', groups);

    /// <summary>
    /// Snapshots the caller's group sequence exactly once: the same array both builds the
    /// cache key and flows into the executor, so a lazy or unstable enumerable can never
    /// produce a key that disagrees with the groups the cached executor was built with.
    /// </summary>
    private static string[] MaterializeGroups(IEnumerable<string>? groups)
        => groups is null ? EmptyGroups : [.. groups];

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The executor generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for open generics and runtime-only registrations.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "Executor types are constructed from typeof expressions below; their constructors are rooted.")]
    private IPipelineExecutor CreateVoidExecutor(Type messageType, string[] groups)
    {
        var descriptor = FindDescriptor(messageType);

        // Generated dispatch roots close the executor generic at compile time; the
        // reflective path below only serves types without a root.
        if (GeneratedDispatchRoots.FindMessage(messageType) is { } root)
        {
            return root.Accept(
                VoidExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups));
        }

        var executorType = typeof(VoidPipelineExecutor<>).MakeGenericType(messageType);

        return (IPipelineExecutor)Activator.CreateInstance(executorType, descriptor, dependenciesFactory, resultAdapterService, groups)!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The executor generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for open generics and runtime-only registrations.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "Executor types are constructed from typeof expressions below; their constructors are rooted.")]
    private object CreateResultExecutor(Type messageType, Type resultType, string[] groups)
    {
        var descriptor = FindDescriptor(messageType);

        if (GeneratedDispatchRoots.FindResult(messageType, resultType) is { } root)
        {
            return root.Accept(
                ResultExecutorVisitor.Instance,
                new ExecutorState(descriptor, dependenciesFactory, resultAdapterService, groups));
        }

        var executorType = typeof(ResultPipelineExecutor<,>).MakeGenericType(messageType, resultType);

        return Activator.CreateInstance(executorType, descriptor, dependenciesFactory, resultAdapterService, groups)!;
    }

    /// <summary>Constructor arguments carried into the generic re-entry of a dispatch root.</summary>
    private readonly record struct ExecutorState(
        IMessageDescriptor Descriptor,
        IMessageDependenciesFactory DependenciesFactory,
        IResultAdapterService? ResultAdapterService,
        string[] Groups);

    /// <summary>
    /// Re-enters a generic context with a root's message type and constructs the closed
    /// void executor there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class VoidExecutorVisitor : IMessageRootVisitor<IPipelineExecutor, ExecutorState>
    {
        public static readonly VoidExecutorVisitor Instance = new();

        public IPipelineExecutor Visit<TMessage>(ExecutorState state) where TMessage : IMessage
            => new VoidPipelineExecutor<TMessage>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups);
    }

    /// <summary>Result-executor counterpart of <see cref="VoidExecutorVisitor"/>.</summary>
    private sealed class ResultExecutorVisitor : IMessageResultRootVisitor<object, ExecutorState>
    {
        public static readonly ResultExecutorVisitor Instance = new();

        public object Visit<TMessage, TResult>(ExecutorState state) where TMessage : IMessage
            => new ResultPipelineExecutor<TMessage, TResult>(
                state.Descriptor, state.DependenciesFactory, state.ResultAdapterService, state.Groups);
    }

    private IMessageDescriptor FindDescriptor(Type messageType)
    {
        return messageResolveStrategy.Find(messageType)
               ?? throw new NoHandlerFoundException(messageType);
    }
}
