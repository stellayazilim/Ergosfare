using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Result-producing pipeline closed over the concrete <typeparamref name="TMessage"/>.
/// Because <typeparamref name="TMessage"/> is the message's runtime type here, the mediation
/// strategy's typed seam always hits — the handler is invoked through its typed member and
/// its <see cref="ValueTask{TResult}"/> never crosses an object-typed bridge.
/// </summary>
internal sealed class ResultPipelineExecutor<TMessage, TResult>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups) : IPipelineExecutor<TResult>
    where TMessage : notnull
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage, TResult> _strategy = new(resultAdapterService);

    public ValueTask<TResult> Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        // The factory caches per (message type, groups) and re-validates against the
        // registry version, mirroring the Mediate path's per-dispatch behavior.
        var dependencies = dependenciesFactory.Create(typeof(TMessage), descriptor, groups);

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }
}

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

    public ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = dependenciesFactory.Create(typeof(TMessage), descriptor, groups);

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
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

    public IPipelineExecutor GetVoidExecutor(Type messageType, IEnumerable<string>? groups = null)
    {
        var key = (messageType, GroupsKey(groups));
        if (!_voidExecutors.TryGetValue(key, out var executor))
        {
            executor = _voidExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateVoidExecutor(k.MessageType, state.Groups),
                (Cache: this, Groups: MaterializeGroups(groups)));
        }

        return executor;
    }

    public IPipelineExecutor<TResult> GetExecutor<TResult>(Type messageType, IEnumerable<string>? groups = null)
    {
        var key = (messageType, typeof(TResult), GroupsKey(groups));
        if (!_resultExecutors.TryGetValue(key, out var executor))
        {
            executor = _resultExecutors.GetOrAdd(key,
                static (k, state) => state.Cache.CreateResultExecutor(k.MessageType, k.ResultType, state.Groups),
                (Cache: this, Groups: MaterializeGroups(groups)));
        }

        return (IPipelineExecutor<TResult>)executor;
    }

    private static string GroupsKey(IEnumerable<string>? groups)
        => groups is null ? string.Empty : string.Join('\x1f', groups);

    private static string[] MaterializeGroups(IEnumerable<string>? groups)
        => groups is null ? EmptyGroups : [.. groups];

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The executor generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Executors close over reference message types (shared generic code under Native AOT); " +
                        "source-generated dispatch will emit these instantiations concretely.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "Executor types are constructed from typeof expressions below; their constructors are rooted.")]
    private IPipelineExecutor CreateVoidExecutor(Type messageType, string[] groups)
    {
        var descriptor = FindDescriptor(messageType);
        var executorType = typeof(VoidPipelineExecutor<>).MakeGenericType(messageType);

        return (IPipelineExecutor)Activator.CreateInstance(executorType, descriptor, dependenciesFactory, resultAdapterService, groups)!;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The executor generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Executors close over reference message types (shared generic code under Native AOT); " +
                        "source-generated dispatch will emit these instantiations concretely.")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "Executor types are constructed from typeof expressions below; their constructors are rooted.")]
    private object CreateResultExecutor(Type messageType, Type resultType, string[] groups)
    {
        var descriptor = FindDescriptor(messageType);
        var executorType = typeof(ResultPipelineExecutor<,>).MakeGenericType(messageType, resultType);

        return Activator.CreateInstance(executorType, descriptor, dependenciesFactory, resultAdapterService, groups)!;
    }

    private IMessageDescriptor FindDescriptor(Type messageType)
    {
        return messageResolveStrategy.Find(messageType)
               ?? throw new NoHandlerFoundException(messageType);
    }
}
