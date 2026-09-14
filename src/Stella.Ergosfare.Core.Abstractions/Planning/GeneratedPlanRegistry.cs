
using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Abstractions.Planning;

/// <summary>
/// The process-wide store of executable generated plans and registration descriptors.
/// Generated registration code fills it as assemblies load.
/// </summary>
/// <remarks>
/// Generated plans implement their execution contracts directly, including their closed
/// generic types. Registration stores those plan instances without building an executor.
/// A dispatch without an executable generated plan fails; there is no reflective fallback.
/// </remarks>
public static class GeneratedPlanRegistry
{
    private static readonly ConcurrentDictionary<(byte Module, string Pattern), Type[]> GeneratedSelections = new();

    /// <summary>Stores the exact participant selection emitted for a source declaration.</summary>
    public static void AddGeneratedSelection(byte module, string pattern, Type[] types)
        => GeneratedSelections[(module, pattern)] = types;

    /// <summary>Applies an existing compiled selection during container configuration.</summary>
    public static void ApplyGeneratedSelection(DispatchPlanCatalog catalog, byte module, string pattern)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(pattern);
        if (!GeneratedSelections.TryGetValue((module, pattern), out var types))
            throw new InvalidOperationException("No compiled Ergosfare selection exists for this AddGenerated call. Ensure the composition root generates plans for this configuration.");
        catalog.Select(types);
    }

    // Enumerated during registration only. The values are the generated plans themselves.
    internal static IEnumerable<(Type MessageType, Type? ResultType, byte Kind, IReadOnlyList<string> Groups, ICompiledPlan Plan)> PlanEntries
    {
        get
        {
            foreach (var pair in StagedVoidPlans)
                yield return (pair.Key.MessageType, null, 0, pair.Key.Groups.Groups, pair.Value);
            foreach (var pair in StagedResultPlans)
                yield return (pair.Key.MessageType, pair.Key.ResultType, 1, pair.Key.Groups.Groups, pair.Value);
            foreach (var pair in BroadcastPlans)
                yield return (pair.Key.MessageType, null, 2, pair.Key.Groups.Groups, pair.Value);
            foreach (var pair in StagedStreamPlans)
                yield return (pair.Key.MessageType, pair.Key.ResultType, 3, Array.Empty<string>(), pair.Value);
        }
    }
    private static readonly ConcurrentDictionary<(Type MessageType, PlanGroupKey Groups), StagedVoidPlan> StagedVoidPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, PlanGroupKey Groups), StagedBroadcastPlan> BroadcastPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType, PlanGroupKey Groups), StagedResultPlan> StagedResultPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), StagedStreamPlan> StagedStreamPlans = new();


    /// <summary>
    /// Roots a staged plan for a void message whose pipeline has interceptor stages: the
    /// whole pipeline as straight-line generated code. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <remarks>
    /// Registration binds a plan only when its descriptor matches the selected composition.
    /// An incompatible plan causes dispatch to fail; no alternative pipeline is built.
    /// </remarks>
    public static void AddStagedPlan<TMessage>(StagedVoidPlan<TMessage> plan)
        where TMessage : IMessage
        => StagedVoidPlans.TryAdd((typeof(TMessage), PlanGroupKey.Default), plan);

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
        => StagedVoidPlans.TryAdd((typeof(TMessage), new PlanGroupKey(groups)), plan);

    /// <summary>
    /// Roots a staged plan for a result-producing message. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the pipeline produces.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddStagedPlan<TMessage, TResult>(StagedResultPlan<TMessage, TResult> plan)
        where TMessage : IMessage
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult), PlanGroupKey.Default), plan);

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
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult), new PlanGroupKey(groups)), plan);

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
        => BroadcastPlans.TryAdd((typeof(TEvent), PlanGroupKey.Default), plan);

    /// <summary>
    /// Roots a broadcast plan for one group set; see the ungrouped overload. Repeated calls
    /// do nothing.
    /// </summary>
    /// <typeparam name="TEvent">The event the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    /// <param name="groups">The groups the plan was compiled for.</param>
    public static void AddBroadcastPlan<TEvent>(StagedBroadcastPlan<TEvent> plan, string[] groups)
        where TEvent : notnull
        => BroadcastPlans.TryAdd((typeof(TEvent), new PlanGroupKey(groups)), plan);

    /// <summary>
    /// Returns the staged void plan for the message's default pipeline, or <c>null</c> when
    /// none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    public static StagedVoidPlan? FindStagedVoidPlan(Type messageType)
        => StagedVoidPlans.TryGetValue((messageType, PlanGroupKey.Default), out var plan) ? plan : null;

    /// <summary>
    /// Returns the staged void plan for a group set, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    public static StagedVoidPlan? FindStagedVoidPlan(Type messageType, IReadOnlyList<string> groups)
        => StagedVoidPlans.TryGetValue((messageType, new PlanGroupKey(groups)), out var plan) ? plan : null;

    /// <summary>
    /// Returns the broadcast plan for the event's default pipeline, or <c>null</c> when
    /// none was generated.
    /// </summary>
    /// <param name="messageType">The event type to look up.</param>
    public static StagedBroadcastPlan? FindBroadcastPlan(Type messageType)
        => BroadcastPlans.TryGetValue((messageType, PlanGroupKey.Default), out var plan) ? plan : null;

    /// <summary>
    /// Returns the broadcast plan for a group set, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The event type to look up.</param>
    /// <param name="groups">The groups the publish asked for.</param>
    public static StagedBroadcastPlan? FindBroadcastPlan(Type messageType, IReadOnlyList<string> groups)
        => BroadcastPlans.TryGetValue((messageType, new PlanGroupKey(groups)), out var plan) ? plan : null;

    /// <summary>
    /// Returns the staged result plan for the pair's default pipeline, or <c>null</c> when
    /// none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    public static StagedResultPlan? FindStagedResultPlan(Type messageType, Type resultType)
        => StagedResultPlans.TryGetValue((messageType, resultType, PlanGroupKey.Default), out var plan) ? plan : null;

    /// <summary>
    /// Returns the staged result plan for a group set, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    /// <param name="groups">The groups the dispatch asked for.</param>
    public static StagedResultPlan? FindStagedResultPlan(Type messageType, Type resultType, IReadOnlyList<string> groups)
        => StagedResultPlans.TryGetValue((messageType, resultType, new PlanGroupKey(groups)), out var plan) ? plan : null;

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
        => StagedVoidPlans.TryAdd((typeof(TMessage), PlanGroupKey.Filtering), plan);

    /// <summary>
    /// Roots the group-filtering plan for a result-producing message; see
    /// <see cref="AddFilteredPlan{TMessage}"/>. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TMessage">The message the plan serves.</typeparam>
    /// <typeparam name="TResult">The result the pipeline produces.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddFilteredPlan<TMessage, TResult>(StagedResultPlan<TMessage, TResult> plan)
        where TMessage : IMessage
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult), PlanGroupKey.Filtering), plan);

    /// <summary>
    /// Roots the group-filtering plan for a broadcast; see
    /// <see cref="AddFilteredPlan{TMessage}"/>. Repeated calls do nothing.
    /// </summary>
    /// <typeparam name="TEvent">The event the plan serves.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddFilteredBroadcastPlan<TEvent>(StagedBroadcastPlan<TEvent> plan)
        where TEvent : notnull
        => BroadcastPlans.TryAdd((typeof(TEvent), PlanGroupKey.Filtering), plan);

    /// <summary>
    /// Returns the group-filtering void plan, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    public static StagedVoidPlan? FindFilteredVoidPlan(Type messageType)
        => StagedVoidPlans.TryGetValue((messageType, PlanGroupKey.Filtering), out var plan) ? plan : null;

    /// <summary>
    /// Returns the group-filtering result plan, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The message type to look up.</param>
    /// <param name="resultType">The result type to look up.</param>
    public static StagedResultPlan? FindFilteredResultPlan(Type messageType, Type resultType)
        => StagedResultPlans.TryGetValue((messageType, resultType, PlanGroupKey.Filtering), out var plan) ? plan : null;

    /// <summary>
    /// Returns the group-filtering broadcast plan, or <c>null</c> when none was generated.
    /// </summary>
    /// <param name="messageType">The event type to look up.</param>
    public static StagedBroadcastPlan? FindFilteredBroadcastPlan(Type messageType)
        => BroadcastPlans.TryGetValue((messageType, PlanGroupKey.Filtering), out var plan) ? plan : null;

    /// <summary>
    /// Registers the staged stream plan of a (query, item) pair.
    /// </summary>
    /// <typeparam name="TQuery">The streaming query the plan serves.</typeparam>
    /// <typeparam name="TResult">The type of the items it streams.</typeparam>
    /// <param name="plan">The generated plan.</param>
    public static void AddStreamPlan<TQuery, TResult>(StagedStreamPlan<TQuery, TResult> plan)
        where TQuery : notnull
        => StagedStreamPlans.TryAdd((typeof(TQuery), typeof(TResult)), plan);

    /// <summary>
    /// Returns the staged stream plan of a (query, item) pair, or <c>null</c> when none was
    /// generated.
    /// </summary>
    /// <param name="messageType">The query type to look up.</param>
    /// <param name="resultType">The item type to look up.</param>
    public static StagedStreamPlan? FindStagedStreamPlan(Type messageType, Type resultType)
        => StagedStreamPlans.TryGetValue((messageType, resultType), out var plan) ? plan : null;


    private static readonly ConcurrentDictionary<Type, PipelineDescriptor> PipelineDescriptors = new();
    private static readonly ConcurrentDictionary<Type, PipelineDescriptor?> PipelineDescriptorLadder = new();

    /// <summary>
    /// Roots a message's compiled pipeline composition. Generated module initializers call
    /// this as assemblies load, and an entry never changes afterwards. Repeated calls do
    /// nothing.
    /// </summary>
    /// <param name="composition">The composition to add.</param>
    public static void AddPipelineDescriptor(PipelineDescriptor composition)
        => PipelineDescriptors.TryAdd(composition.MessageType, composition);

    /// <summary>
    /// Every composition in the table.
    /// </summary>
    /// <remarks>
    /// The only enumeration offered, and meant for setup-time questions a per-message
    /// lookup cannot answer — chiefly which participant types exist at all, which container
    /// registration intersects with its own selection.
    /// </remarks>
    public static IEnumerable<PipelineDescriptor> PipelineDescriptorEntries => PipelineDescriptors.Values;

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
    /// Closed generic entries take priority over generic-definition metadata. Outcomes are cached
    /// per runtime type, misses included, so an entry added after a type was first resolved
    /// is not picked up for that type — which holds because entries are only added as
    /// assemblies load.
    /// </remarks>
    public static PipelineDescriptor? FindPipelineDescriptor(Type messageType)
    {
        if (PipelineDescriptors.TryGetValue(messageType, out var exact))
        {
            return exact;
        }

        return PipelineDescriptorLadder.GetOrAdd(messageType, static runtimeType =>
        {
            if (runtimeType.IsGenericType
                && PipelineDescriptors.TryGetValue(runtimeType.GetGenericTypeDefinition(), out var definition))
                return definition;

            for (var current = runtimeType.BaseType; current is not null; current = current.BaseType)
            {
                if (PipelineDescriptors.TryGetValue(current, out var closed)) return closed;
                var key = current.IsGenericType ? current.GetGenericTypeDefinition() : current;

                if (PipelineDescriptors.TryGetValue(key, out var entry))
                {
                    return entry;
                }
            }

            return null;
        });
    }
}
