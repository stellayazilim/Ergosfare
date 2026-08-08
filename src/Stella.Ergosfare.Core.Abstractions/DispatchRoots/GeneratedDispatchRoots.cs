using System;
using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Process-wide store of generically instantiated dispatch roots, populated by
/// source-generated registration code. Each root closes a dispatch generic over a concrete
/// message (and result) type at compile time, letting the dispatch caches construct their
/// pipeline executors and invokers without <see cref="Type.MakeGenericType"/> — and giving
/// Native AOT and trimming a static anchor for every instantiation, value-type messages
/// and results included, which shared generic code cannot cover. The reflective
/// <c>MakeGenericType</c> paths remain as the fallback for types without a root (open
/// generics, runtime-only registrations).
/// </summary>
public static class GeneratedDispatchRoots
{
    private static readonly ConcurrentDictionary<Type, MessageRoot> Messages = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), MessageResultRoot> Results = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), MessageResultRoot> Streams = new();
    private static readonly ConcurrentDictionary<Type, VoidPlanRoot> VoidPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), ResultPlanRoot> ResultPlans = new();
    private static readonly ConcurrentDictionary<Type, StagedVoidPlan> StagedVoidPlans = new();
    private static readonly ConcurrentDictionary<(Type MessageType, Type ResultType), StagedResultPlan> StagedResultPlans = new();

    /// <summary>Roots the void dispatch generics of a message type. Idempotent.</summary>
    public static void AddMessage<TMessage>() where TMessage : IMessage
        => Messages.TryAdd(typeof(TMessage), new MessageRoot<TMessage>());

    /// <summary>Roots the result-producing dispatch generics of a message type. Idempotent.</summary>
    public static void AddResult<TMessage, TResult>() where TMessage : IMessage
        => Results.TryAdd((typeof(TMessage), typeof(TResult)), new MessageResultRoot<TMessage, TResult>());

    /// <summary>Roots the streaming dispatch generics of a message type. Idempotent.</summary>
    public static void AddStream<TMessage, TResult>() where TMessage : IMessage
        => Streams.TryAdd((typeof(TMessage), typeof(TResult)), new MessageResultRoot<TMessage, TResult>());

    /// <summary>The void dispatch root of the message type, or <c>null</c> when none was generated.</summary>
    public static MessageRoot? FindMessage(Type messageType)
        => Messages.TryGetValue(messageType, out var root) ? root : null;

    /// <summary>The result dispatch root of the (message, result) pair, or <c>null</c> when none was generated.</summary>
    public static MessageResultRoot? FindResult(Type messageType, Type resultType)
        => Results.TryGetValue((messageType, resultType), out var root) ? root : null;

    /// <summary>The stream dispatch root of the (message, result) pair, or <c>null</c> when none was generated.</summary>
    public static MessageResultRoot? FindStream(Type messageType, Type resultType)
        => Streams.TryGetValue((messageType, resultType), out var root) ? root : null;

    /// <summary>
    /// Roots a compile-time pipeline plan for a void message whose entire pipeline is a
    /// single async handler: the dispatch executor closes over both the message and the
    /// handler type, so the handler is invoked devirtualized — no contract pattern match.
    /// The plan is advisory: the executor re-validates the actual pipeline against the
    /// registry on every version change and falls back to the runtime dispatch shape
    /// whenever the pipeline no longer matches (interceptors registered at runtime, a
    /// different handler resolved, adapters configured). Idempotent.
    /// </summary>
    public static void AddVoidPlan<TMessage, THandler>()
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage>
        => VoidPlans.TryAdd(typeof(TMessage), new VoidPlanRoot<TMessage, THandler>());

    /// <summary>
    /// Variant of <see cref="AddVoidPlan{TMessage, THandler}()"/> carrying a compile-time
    /// construction path for the handler: the generator emits
    /// <c>static () => new THandler()</c> for handlers with an accessible parameterless
    /// constructor that are not disposable. The factory is advisory like the plan itself —
    /// the executor uses it only after verifying at runtime that the handler's DI
    /// registration is the module's own plain transient one (no user factory, no lifetime
    /// override, not memoized), where container resolution and direct construction are
    /// semantically identical. Idempotent.
    /// </summary>
    public static void AddVoidPlan<TMessage, THandler>(Func<THandler> directHandlerFactory)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage>
        => VoidPlans.TryAdd(typeof(TMessage), new VoidPlanRoot<TMessage, THandler>(directHandlerFactory));

    /// <summary>
    /// Variant of <see cref="AddVoidPlan{TMessage, THandler}()"/> carrying a compile-time
    /// construction path for handlers with constructor dependencies: the generator emits
    /// <c>static provider =&gt; new THandler(provider.GetRequiredService&lt;TDep&gt;(), ...)</c>
    /// for handlers whose single public constructor takes only plain (or
    /// <c>[FromKeyedServices]</c>) service parameters — the one shape where the container's
    /// own constructor selection and the emitted construction provably coincide. The same
    /// advisory contract applies: the executor uses the factory only after verifying the
    /// handler's DI registration is the module's own plain transient one, and the
    /// dependencies resolve from the dispatching scope's provider exactly as container
    /// activation would resolve them. Idempotent.
    /// </summary>
    public static void AddVoidPlan<TMessage, THandler>(Func<IServiceProvider, THandler> directHandlerFactory)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage>
        => VoidPlans.TryAdd(typeof(TMessage), new VoidPlanRoot<TMessage, THandler>(directHandlerFactory));

    /// <summary>The void pipeline plan of the message type, or <c>null</c> when none was generated.</summary>
    public static VoidPlanRoot? FindVoidPlan(Type messageType)
        => VoidPlans.TryGetValue(messageType, out var root) ? root : null;

    /// <summary>
    /// Result-producing counterpart of <see cref="AddVoidPlan{TMessage, THandler}()"/>:
    /// roots a compile-time pipeline plan for a message whose entire pipeline is a single
    /// async result handler, so the dispatch executor invokes it devirtualized. The plan
    /// is advisory and re-validated per registry version exactly like the void plan.
    /// Idempotent.
    /// </summary>
    public static void AddResultPlan<TMessage, TResult, THandler>()
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>
        => ResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), new ResultPlanRoot<TMessage, TResult, THandler>());

    /// <summary>
    /// Variant of <see cref="AddResultPlan{TMessage, TResult, THandler}()"/> carrying the
    /// compile-time handler construction path; see
    /// <see cref="AddVoidPlan{TMessage, THandler}(Func{THandler})"/> for the contract.
    /// Idempotent.
    /// </summary>
    public static void AddResultPlan<TMessage, TResult, THandler>(Func<THandler> directHandlerFactory)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>
        => ResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), new ResultPlanRoot<TMessage, TResult, THandler>(directHandlerFactory));

    /// <summary>
    /// Variant of <see cref="AddResultPlan{TMessage, TResult, THandler}()"/> carrying the
    /// compile-time construction path for handlers with constructor dependencies; see
    /// <see cref="AddVoidPlan{TMessage, THandler}(Func{IServiceProvider, THandler})"/> for
    /// the contract. Idempotent.
    /// </summary>
    public static void AddResultPlan<TMessage, TResult, THandler>(Func<IServiceProvider, THandler> directHandlerFactory)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>
        => ResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), new ResultPlanRoot<TMessage, TResult, THandler>(directHandlerFactory));

    /// <summary>The result pipeline plan of the (message, result) pair, or <c>null</c> when none was generated.</summary>
    public static ResultPlanRoot? FindResultPlan(Type messageType, Type resultType)
        => ResultPlans.TryGetValue((messageType, resultType), out var root) ? root : null;

    /// <summary>
    /// Roots a staged pipeline plan for a void message whose pipeline carries interceptor
    /// stages: bespoke straight-line code for the whole pipeline, replacing the runtime
    /// strategy's generic machinery. Advisory exactly like the single-handler plans — the
    /// hosting executor re-validates the plan's <see cref="StagedPlanComposition"/> against
    /// the registry per version and falls back to the runtime strategy on any mismatch.
    /// Idempotent.
    /// </summary>
    public static void AddStagedPlan<TMessage>(StagedVoidPlan<TMessage> plan)
        where TMessage : notnull, IMessage
        => StagedVoidPlans.TryAdd(typeof(TMessage), plan);

    /// <summary>
    /// Result-producing counterpart of <see cref="AddStagedPlan{TMessage}"/>. Idempotent.
    /// </summary>
    public static void AddStagedPlan<TMessage, TResult>(StagedResultPlan<TMessage, TResult> plan)
        where TMessage : notnull, IMessage
        => StagedResultPlans.TryAdd((typeof(TMessage), typeof(TResult)), plan);

    /// <summary>The staged void plan of the message type, or <c>null</c> when none was generated.</summary>
    public static StagedVoidPlan? FindStagedVoidPlan(Type messageType)
        => StagedVoidPlans.TryGetValue(messageType, out var plan) ? plan : null;

    /// <summary>The staged result plan of the (message, result) pair, or <c>null</c> when none was generated.</summary>
    public static StagedResultPlan? FindStagedResultPlan(Type messageType, Type resultType)
        => StagedResultPlans.TryGetValue((messageType, resultType), out var plan) ? plan : null;
}
