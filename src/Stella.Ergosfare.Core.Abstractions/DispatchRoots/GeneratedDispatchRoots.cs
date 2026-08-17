
using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// The process-wide store of everything the source generator compiled for dispatch:
/// generic instantiations closed over concrete message and result types, pipeline plans,
/// and message compositions. Generated registration code fills it as assemblies load.
/// </summary>
/// <remarks>
/// A generated root lets the dispatch caches build their executors without
/// <see cref="Type.MakeGenericType"/>, and gives trimming and Native AOT a static anchor
/// for every instantiation — including ones over value types, which shared generic code
/// cannot cover. Types without a root, such as open generics and runtime-only
/// registrations, fall back to reflective construction.
/// </remarks>
public static class GeneratedDispatchRoots
{
    private static readonly ConcurrentDictionary<Type, MessageRoot> Messages = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), MessageResultRoot> Results = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), MessageResultRoot> Streams = new();
    private static readonly ConcurrentDictionary<Type, VoidHandlerPlan> VoidPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), ResultHandlerPlan> ResultPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, string Groups), StagedVoidPlan> StagedVoidPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, string Groups), StagedBroadcastPlan> BroadcastPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType, string Groups), StagedResultPlan> StagedResultPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), object> ResultAdapters = new();
    private static readonly ConcurrentDictionary<Type, object> DefaultResultAdapters = new();
    private static readonly ConcurrentDictionary<Type, object?> IgnoredResultAdapters = new();
    private static volatile bool _resultAdaptersSealed;

    /// <summary>
    /// Roots the void dispatch generics of a message type. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message type to root.</typeparam>
    public static void AddMessage<TMessage>() where TMessage : IMessage
        => Messages.TryAdd(typeof(TMessage), new MessageRoot<TMessage>());

    /// <summary>
    /// Roots the result-producing dispatch generics of a message type. Repeated calls do
    /// nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message type to root.</typeparam>
    /// <typeparam name="TResult">The result type to root it with.</typeparam>
    public static void AddResult<TMessage, TResult>() where TMessage : IMessage
        => Results.TryAdd((typeof(TMessage), typeof(TResult)), new MessageResultRoot<TMessage, TResult>());

    /// <summary>
    /// Roots the streaming dispatch generics of a message type. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message type to root.</typeparam>
    /// <typeparam name="TResult">The streamed item type.</typeparam>
    public static void AddStream<TMessage, TResult>() where TMessage : IMessage
        => Streams.TryAdd((typeof(TMessage), typeof(TResult)), new MessageResultRoot<TMessage, TResult>());

    /// <summary>
    /// Returns the void dispatch root of <paramref name="messageType"/>, or <c>null</c>
    /// when none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    public static MessageRoot? FindMessage(Type messageType)
        => Messages.TryGetValue(messageType, out var root) ? root : null;

    /// <summary>
    /// Returns the result dispatch root of the pair, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    public static MessageResultRoot? FindResult(Type messageType, Type resultType)
        => Results.TryGetValue((messageType, resultType), out var root) ? root : null;

    /// <summary>
    /// Returns the stream dispatch root of the pair, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The query type to look up.</param>
    /// <param name="resultType">The streamed item type to look up.</param>
    public static MessageResultRoot? FindStream(Type messageType, Type resultType)
        => Streams.TryGetValue((messageType, resultType), out var root) ? root : null;

    /// <summary>
    /// Roots the adapter a message's <c>[ResultAdapter]</c> annotation binds to one of its
    /// result slots. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message carrying the annotation.</typeparam>
    /// <typeparam name="TResult">The result slot the adapter serves.</typeparam>
    /// <typeparam name="TAdapter">The annotated adapter.</typeparam>
    /// <remarks>
    /// The constraints are the whole point: naming an adapter that does not serve the slot,
    /// or that cannot be constructed, is a compile error in the generated file rather than a
    /// reflective test at first dispatch. What reaches the runtime is an instance already
    /// typed as the slot's adapter, so binding is a dictionary read and the pipeline's use of
    /// it is one interface call.
    /// </remarks>
    public static void AddResultAdapter<TMessage, TResult, TAdapter>()
        where TAdapter : IResultAdapter<TResult>, new()
        => ResultAdapters.TryAdd((typeof(TMessage), typeof(TResult)), new TAdapter());

    /// <summary>
    /// Roots a message's opt-out of result adaptation. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message carrying <c>[IgnoreResultAdapter]</c>.</typeparam>
    /// <remarks>
    /// The opt-out is inherited, so what is written here is the verdict of the generator's
    /// walk up the base chain rather than one attribute — which is why the runtime never has
    /// to walk it again.
    /// </remarks>
    public static void AddIgnoredResultAdapter<TMessage>()
        => IgnoredResultAdapters.TryAdd(typeof(TMessage), null);

    /// <summary>
    /// Whether the message opted out of result adaptation.
    /// </summary>
    /// <typeparam name="TMessage">The message to look up.</typeparam>
    public static bool IsResultAdapterIgnored<TMessage>()
        => IgnoredResultAdapters.ContainsKey(typeof(TMessage));

    /// <summary>
    /// Roots the container-wide default adapter's answer for one result type. Repeated calls
    /// do nothing.
    /// </summary>
    /// <typeparam name="TResult">The result type served.</typeparam>
    /// <typeparam name="TAdapter">
    /// The adapter serving it — the configured type itself, or the closing of an open
    /// definition over this result type.
    /// </typeparam>
    /// <remarks>
    /// A compilation names one default adapter, so this table needs no key for which adapter
    /// answered: the unification an open definition needs is done at compile time, and the
    /// closed form arrives here already built.
    /// </remarks>
    public static void AddDefaultResultAdapter<TResult, TAdapter>()
        where TAdapter : IResultAdapter<TResult>, new()
        => DefaultResultAdapters.TryAdd(typeof(TResult), new TAdapter());

    /// <summary>
    /// Declares that generated registration filled the adapter tables, so a slot missing
    /// from them is an answer rather than an absence.
    /// </summary>
    /// <remarks>
    /// Without it the two are indistinguishable: a pair with no entry could be a message
    /// whose adapter serves another slot, or an application the generator never ran for. The
    /// seal makes the first case a silent <c>null</c> and the second an actionable throw.
    /// </remarks>
    public static void SealResultAdapters() => _resultAdaptersSealed = true;

    /// <summary>
    /// Whether generated registration has filled the adapter tables in this process.
    /// </summary>
    public static bool ResultAdaptersSealed => _resultAdaptersSealed;

    /// <summary>
    /// Returns the adapter a message's annotation binds to a result slot, or <c>null</c>
    /// when the annotation serves another slot or the message carries none.
    /// </summary>
    /// <typeparam name="TMessage">The message to look up.</typeparam>
    /// <typeparam name="TResult">The result slot to look up.</typeparam>
    public static IResultAdapter<TResult>? FindResultAdapter<TMessage, TResult>()
        => ResultAdapters.TryGetValue((typeof(TMessage), typeof(TResult)), out var adapter)
            ? (IResultAdapter<TResult>)adapter
            : null;

    /// <summary>
    /// Returns the default adapter's answer for a result type, or <c>null</c> when the
    /// configured default does not serve it.
    /// </summary>
    /// <typeparam name="TResult">The result type to look up.</typeparam>
    public static IResultAdapter<TResult>? FindDefaultResultAdapter<TResult>()
        => DefaultResultAdapters.TryGetValue(typeof(TResult), out var adapter)
            ? (IResultAdapter<TResult>)adapter
            : null;

    /// <summary>
    /// Roots a plan for a void message whose whole pipeline is one asynchronous handler,
    /// letting the executor invoke that handler directly. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="THandler">The handler the plan invokes.</typeparam>
    /// <remarks>
    /// The plan is a proposal, not an instruction: the executor checks it against the
    /// composition this container actually selected and falls back to the general dispatch
    /// path whenever the two differ.
    /// </remarks>
    public static void AddVoidPlan<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage>
        => VoidPlans.TryAdd(typeof(TMessage), new VoidHandlerPlan<TMessage, THandler>());

    /// <summary>
    /// Roots a void plan that also carries a way to construct the handler without asking
    /// the container. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="THandler">The handler the plan invokes.</typeparam>
    /// <param name="directHandlerFactory">Constructs the handler.</param>
    /// <remarks>
    /// Emitted for handlers that have an accessible parameterless constructor and are not
    /// disposable. Like the plan itself the factory is a proposal: the executor uses it only
    /// after confirming the handler's registration is the module's own plain transient one —
    /// no user factory, no lifetime override, not memoized — where constructing and
    /// resolving mean the same thing.
    /// </remarks>
    public static void AddVoidPlan<TMessage, THandler>(Func<THandler> directHandlerFactory)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage>
        => VoidPlans.TryAdd(typeof(TMessage), new VoidHandlerPlan<TMessage, THandler>(directHandlerFactory));

    /// <summary>
    /// Roots a void plan carrying a construction path for a handler that takes constructor
    /// dependencies. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="THandler">The handler the plan invokes.</typeparam>
    /// <param name="directHandlerFactory">
    /// Constructs the handler, resolving its dependencies from the dispatching scope's
    /// provider.
    /// </param>
    /// <remarks>
    /// Emitted only for handlers with a single public constructor taking plain or
    /// <c>[FromKeyedServices]</c> service parameters — the shape where the container's own
    /// constructor selection and this construction provably agree. The same conditions as
    /// the parameterless overload apply before the executor uses it.
    /// </remarks>
    public static void AddVoidPlan<TMessage, THandler>(Func<IServiceProvider, THandler> directHandlerFactory)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage>
        => VoidPlans.TryAdd(typeof(TMessage), new VoidHandlerPlan<TMessage, THandler>(directHandlerFactory));

    /// <summary>
    /// Returns the void pipeline plan of <paramref name="messageType"/>, or <c>null</c>
    /// when none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    public static VoidHandlerPlan? FindVoidPlan(Type messageType)
        => VoidPlans.TryGetValue(messageType, out var root) ? root : null;

    /// <summary>
    /// Roots a plan for a message whose whole pipeline is one asynchronous result handler;
    /// the result-producing counterpart of
    /// <see cref="AddVoidPlan{TMessage, THandler}()"/>. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the handler produces.</typeparam>
    /// <typeparam name="THandler">The handler the plan invokes.</typeparam>
    public static void AddResultPlan<TMessage, TResult, THandler>()
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>
        => ResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), new ResultHandlerPlan<TMessage, TResult, THandler>());

    /// <summary>
    /// Roots a result plan that also carries a way to construct the handler; see
    /// <see cref="AddVoidPlan{TMessage, THandler}(Func{THandler})"/> for when the factory
    /// is used. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the handler produces.</typeparam>
    /// <typeparam name="THandler">The handler the plan invokes.</typeparam>
    /// <param name="directHandlerFactory">Constructs the handler.</param>
    public static void AddResultPlan<TMessage, TResult, THandler>(Func<THandler> directHandlerFactory)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>
        => ResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), new ResultHandlerPlan<TMessage, TResult, THandler>(directHandlerFactory));

    /// <summary>
    /// Roots a result plan carrying a construction path for a handler with constructor
    /// dependencies; see
    /// <see cref="AddVoidPlan{TMessage, THandler}(Func{IServiceProvider, THandler})"/> for
    /// the conditions. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the handler produces.</typeparam>
    /// <typeparam name="THandler">The handler the plan invokes.</typeparam>
    /// <param name="directHandlerFactory">
    /// Constructs the handler, resolving its dependencies from the dispatching scope's
    /// provider.
    /// </param>
    public static void AddResultPlan<TMessage, TResult, THandler>(Func<IServiceProvider, THandler> directHandlerFactory)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>
        => ResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), new ResultHandlerPlan<TMessage, TResult, THandler>(directHandlerFactory));

    /// <summary>
    /// Returns the result pipeline plan of the pair, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    public static ResultHandlerPlan? FindResultPlan(Type messageType, Type resultType)
        => ResultPlans.TryGetValue((messageType, resultType), out var root) ? root : null;

    /// <summary>
    /// Roots a staged plan for a void message whose pipeline has interceptor stages: the
    /// whole pipeline as straight-line generated code. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <remarks>
    /// A proposal like the single-handler plans: the executor checks the plan's
    /// <see cref="StagedPlanKey"/> against the composition this container selected and falls
    /// back to the general strategy on any difference.
    /// </remarks>
    public static void AddStagedPlan<TMessage>(StagedVoidPlan<TMessage> plan)
        where TMessage : IMessage
        => StagedVoidPlans.TryAdd((typeof(TMessage), string.Empty), plan);

    /// <summary>
    /// Roots a staged void plan for one group set. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <param name="groups">The groups the plan was compiled for.</param>
    /// <remarks>
    /// The requested groups help decide which participants run, so they are part of what
    /// identifies a plan, just as the message type is.
    /// </remarks>
    public static void AddStagedPlan<TMessage>(StagedVoidPlan<TMessage> plan, string[] groups)
        where TMessage : IMessage
        => StagedVoidPlans.TryAdd((typeof(TMessage), GroupKey(groups)), plan);

    /// <summary>
    /// Roots a staged plan for a result-producing message. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the pipeline produces.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddStagedPlan<TMessage, TResult>(StagedResultPlan<TMessage, TResult> plan)
        where TMessage : IMessage
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult), string.Empty), plan);

    /// <summary>
    /// Roots a staged result plan for one group set; see the void overload. Repeated calls
    /// do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the pipeline produces.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <param name="groups">The groups the plan was compiled for.</param>
    public static void AddStagedPlan<TMessage, TResult>(StagedResultPlan<TMessage, TResult> plan, string[] groups)
        where TMessage : IMessage
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult), GroupKey(groups)), plan);

    /// <summary>
    /// Roots a staged plan for a broadcast. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TEvent">The event the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <remarks>
    /// Broadcast plans have their own store rather than sharing the void one: a publish
    /// looks here and a send looks there, so which store answered already settles how
    /// delivery differs.
    /// </remarks>
    public static void AddBroadcastPlan<TEvent>(StagedBroadcastPlan<TEvent> plan)
        where TEvent : notnull
        => BroadcastPlans.TryAdd((typeof(TEvent), string.Empty), plan);

    /// <summary>
    /// Roots a broadcast plan for one group set; see the ungrouped overload. Repeated calls
    /// do nothing.
    /// </summary>
    /// <typeparam name="TEvent">The event the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <param name="groups">The groups the plan was compiled for.</param>
    public static void AddBroadcastPlan<TEvent>(StagedBroadcastPlan<TEvent> plan, string[] groups)
        where TEvent : notnull
        => BroadcastPlans.TryAdd((typeof(TEvent), GroupKey(groups)), plan);

    /// <summary>
    /// Returns the staged void plan for the message's default pipeline, or <c>null</c> when
    /// none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    public static StagedVoidPlan? FindStagedVoidPlan(Type messageType)
        => StagedVoidPlans.TryGetValue((messageType, string.Empty), out var plan) ? plan : null;

    /// <summary>
    /// Returns the staged void plan for a group set, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    public static StagedVoidPlan? FindStagedVoidPlan(Type messageType, IReadOnlyList<string> groups)
        => StagedVoidPlans.TryGetValue((messageType, GroupKey(groups)), out var plan) ? plan : null;

    /// <summary>
    /// Returns the broadcast plan for the event's default pipeline, or <c>null</c> when
    /// none was generated.
    /// </summary>
    /// <param name="messageType">The event type to look up.</param>
    public static StagedBroadcastPlan? FindBroadcastPlan(Type messageType)
        => BroadcastPlans.TryGetValue((messageType, string.Empty), out var plan) ? plan : null;

    /// <summary>
    /// Returns the broadcast plan for a group set, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The event type to look up.</param>
    /// <param name="groups">The groups the publish asked for.</param>
    public static StagedBroadcastPlan? FindBroadcastPlan(Type messageType, IReadOnlyList<string> groups)
        => BroadcastPlans.TryGetValue((messageType, GroupKey(groups)), out var plan) ? plan : null;

    /// <summary>
    /// Returns the staged result plan for the pair's default pipeline, or <c>null</c> when
    /// none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    public static StagedResultPlan? FindStagedResultPlan(Type messageType, Type resultType)
        => StagedResultPlans.TryGetValue((messageType, resultType, string.Empty), out var plan) ? plan : null;

    /// <summary>
    /// Returns the staged result plan for a group set, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    public static StagedResultPlan? FindStagedResultPlan(Type messageType, Type resultType, IReadOnlyList<string> groups)
        => StagedResultPlans.TryGetValue((messageType, resultType, GroupKey(groups)), out var plan) ? plan : null;

    /// <summary>
    /// The key the group-filtering plans are stored under. It opens with a control
    /// character no group name can contain, so it never collides with a real group key.
    /// </summary>
    private const string FilteredPlanKey = "\u0000filtered";

    /// <summary>
    /// Roots the plan that serves dispatches whose group filter is only known at runtime:
    /// one body holding every participant, each call guarded by its own group test.
    /// Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddFilteredPlan<TMessage>(StagedVoidPlan<TMessage> plan)
        where TMessage : IMessage
        => StagedVoidPlans.TryAdd((typeof(TMessage), FilteredPlanKey), plan);

    /// <summary>
    /// Roots the group-filtering plan for a result-producing message; see
    /// <see cref="AddFilteredPlan{TMessage}"/>. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the pipeline produces.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddFilteredPlan<TMessage, TResult>(StagedResultPlan<TMessage, TResult> plan)
        where TMessage : IMessage
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult), FilteredPlanKey), plan);

    /// <summary>
    /// Roots the group-filtering plan for a broadcast; see
    /// <see cref="AddFilteredPlan{TMessage}"/>. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TEvent">The event the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddFilteredBroadcastPlan<TEvent>(StagedBroadcastPlan<TEvent> plan)
        where TEvent : notnull
        => BroadcastPlans.TryAdd((typeof(TEvent), FilteredPlanKey), plan);

    /// <summary>
    /// Returns the group-filtering void plan, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    public static StagedVoidPlan? FindFilteredVoidPlan(Type messageType)
        => StagedVoidPlans.TryGetValue((messageType, FilteredPlanKey), out var plan) ? plan : null;

    /// <summary>
    /// Returns the group-filtering result plan, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    public static StagedResultPlan? FindFilteredResultPlan(Type messageType, Type resultType)
        => StagedResultPlans.TryGetValue((messageType, resultType, FilteredPlanKey), out var plan) ? plan : null;

    /// <summary>
    /// Returns the group-filtering broadcast plan, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The event type to look up.</param>
    public static StagedBroadcastPlan? FindFilteredBroadcastPlan(Type messageType)
        => BroadcastPlans.TryGetValue((messageType, FilteredPlanKey), out var plan) ? plan : null;

    /// <summary>
    /// Builds the canonical key of a group set: sorted ordinally, deduplicated and joined.
    /// </summary>
    /// <param name="groups">The groups to key, or <c>null</c> for the default pipeline.</param>
    /// <returns>The key the matching plan is stored under.</returns>
    /// <remarks>
    /// Group selection is an any-of test, so order and repetition carry no meaning; a key
    /// that preserved them would miss the plan the generator compiled for the same set
    /// spelled differently. Only reached when a per-message slot misses, so the sort is
    /// paid once per (pipeline, group set) rather than per dispatch.
    /// </remarks>
    private static string GroupKey(IReadOnlyList<string>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return string.Empty;
        }

        if (groups.Count == 1)
        {
            return groups[0];
        }

        var names = new string[groups.Count];

        for (var i = 0; i < groups.Count; i++)
        {
            names[i] = groups[i];
        }

        Array.Sort(names, StringComparer.Ordinal);

        var builder = new System.Text.StringBuilder(names[0]);

        for (var i = 1; i < names.Length; i++)
        {
            if (string.Equals(names[i], names[i - 1], StringComparison.Ordinal))
            {
                continue;
            }

            // The unit separator keeps {"ab"} and {"a","b"} from producing the same key.
            builder.Append('\u001f').Append(names[i]);
        }

        return builder.ToString();
    }

    private static readonly ConcurrentDictionary<Type, FrozenComposition> FrozenCompositions = new();
    private static readonly ConcurrentDictionary<Type, FrozenComposition?> FrozenCompositionLadder = new();

    /// <summary>
    /// Roots a message's compiled pipeline composition. Generated module initializers call
    /// this as assemblies load, and an entry never changes afterwards. Repeated calls do
    /// nothing.
    /// </summary>
    /// <param name="composition">The composition to add.</param>
    public static void AddFrozenComposition(FrozenComposition composition)
        => FrozenCompositions.TryAdd(composition.MessageType, composition);

    /// <summary>
    /// Every composition in the table.
    /// </summary>
    /// <remarks>
    /// The only enumeration offered, and meant for setup-time questions a per-message
    /// lookup cannot answer — chiefly which participant types exist at all, which container
    /// registration intersects with its own selection.
    /// </remarks>
    public static IEnumerable<FrozenComposition> FrozenCompositionEntries => FrozenCompositions.Values;

    /// <summary>
    /// Returns the composition serving a runtime message type.
    /// </summary>
    /// <param name="messageType">The dispatched message's runtime type.</param>
    /// <returns>
    /// The exact entry when there is one; otherwise the nearest entry up the type's
    /// ancestor chain, which is how runtime-generated subtypes such as ORM proxies and
    /// mocks are served. <c>null</c> when no ancestor has an entry either.
    /// </returns>
    /// <remarks>
    /// A generic runtime type is looked up by its generic definition. Outcomes are cached
    /// per runtime type, misses included, so an entry added after a type was first resolved
    /// is not picked up for that type — which holds because entries are only added as
    /// assemblies load.
    /// </remarks>
    public static FrozenComposition? FindFrozenComposition(Type messageType)
    {
        if (messageType.IsGenericType)
        {
            messageType = messageType.GetGenericTypeDefinition();
        }

        if (FrozenCompositions.TryGetValue(messageType, out var exact))
        {
            return exact;
        }

        return FrozenCompositionLadder.GetOrAdd(messageType, static runtimeType =>
        {
            for (var current = runtimeType.BaseType; current is not null; current = current.BaseType)
            {
                var key = current.IsGenericType ? current.GetGenericTypeDefinition() : current;

                if (FrozenCompositions.TryGetValue(key, out var entry))
                {
                    return entry;
                }
            }

            return null;
        });
    }
}
