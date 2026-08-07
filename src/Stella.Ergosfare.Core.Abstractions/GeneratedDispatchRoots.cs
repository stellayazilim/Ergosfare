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

    /// <summary>The result pipeline plan of the (message, result) pair, or <c>null</c> when none was generated.</summary>
    public static ResultPlanRoot? FindResultPlan(Type messageType, Type resultType)
        => ResultPlans.TryGetValue((messageType, resultType), out var root) ? root : null;
}

/// <summary>
/// A dispatch root closed over a concrete message type. A consumer implements
/// <see cref="IMessageRootVisitor{TReturn, TState}"/> to re-enter a generic context with
/// the root's type argument and construct its closed dispatch component there — no
/// reflection involved.
/// </summary>
public abstract class MessageRoot
{
    /// <summary>Invokes the visitor with this root's message type as the generic argument.</summary>
    public abstract TReturn Accept<TReturn, TState>(IMessageRootVisitor<TReturn, TState> visitor, TState state);
}

/// <summary>The concrete closure of <see cref="MessageRoot"/>; instantiated by generated code.</summary>
public sealed class MessageRoot<TMessage> : MessageRoot where TMessage : IMessage
{
    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IMessageRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);
}

/// <summary>Generic re-entry point for consumers of <see cref="MessageRoot"/>.</summary>
public interface IMessageRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the root's message type as the generic argument.</summary>
    TReturn Visit<TMessage>(TState state) where TMessage : IMessage;
}

/// <summary>
/// A dispatch root closed over a concrete (message, result) pair; see
/// <see cref="MessageRoot"/>.
/// </summary>
public abstract class MessageResultRoot
{
    /// <summary>Invokes the visitor with this root's message and result types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IMessageResultRootVisitor<TReturn, TState> visitor, TState state);
}

/// <summary>The concrete closure of <see cref="MessageResultRoot"/>; instantiated by generated code.</summary>
public sealed class MessageResultRoot<TMessage, TResult> : MessageResultRoot where TMessage : IMessage
{
    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IMessageResultRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);
}

/// <summary>Generic re-entry point for consumers of <see cref="MessageResultRoot"/>.</summary>
public interface IMessageResultRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the root's message and result types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult>(TState state) where TMessage : IMessage;
}

/// <summary>
/// A compile-time pipeline plan closed over a void message and its sole async handler;
/// see <see cref="GeneratedDispatchRoots.AddVoidPlan{TMessage, THandler}"/> and
/// <see cref="MessageRoot"/> for the visitor re-entry pattern.
/// </summary>
public abstract class VoidPlanRoot
{
    /// <summary>Invokes the visitor with this plan's message and handler types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IVoidPlanRootVisitor<TReturn, TState> visitor, TState state);

    /// <summary>
    /// The compile-time handler construction path — a <c>Func&lt;THandler&gt;</c> carried
    /// erased, cast back inside the executor's closed generic context — or <c>null</c>
    /// when the generator emitted no factory for the handler.
    /// </summary>
    internal virtual object? DirectHandlerFactory => null;
}

/// <summary>The concrete closure of <see cref="VoidPlanRoot"/>; instantiated by generated code.</summary>
public sealed class VoidPlanRoot<TMessage, THandler> : VoidPlanRoot
    where TMessage : notnull, IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private readonly Func<THandler>? _directHandlerFactory;

    /// <summary>Creates a plan without a compile-time construction path.</summary>
    public VoidPlanRoot()
    {
    }

    internal VoidPlanRoot(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IVoidPlanRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, THandler>(state);
}

/// <summary>Generic re-entry point for consumers of <see cref="VoidPlanRoot"/>.</summary>
public interface IVoidPlanRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message and handler types as the generic arguments.</summary>
    TReturn Visit<TMessage, THandler>(TState state)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage>;
}

/// <summary>
/// A compile-time pipeline plan closed over a result-producing message, its result type
/// and its sole async handler; the result-producing counterpart of
/// <see cref="VoidPlanRoot"/>.
/// </summary>
public abstract class ResultPlanRoot
{
    /// <summary>Invokes the visitor with this plan's message, result and handler types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IResultPlanRootVisitor<TReturn, TState> visitor, TState state);

    /// <inheritdoc cref="VoidPlanRoot.DirectHandlerFactory"/>
    internal virtual object? DirectHandlerFactory => null;
}

/// <summary>The concrete closure of <see cref="ResultPlanRoot"/>; instantiated by generated code.</summary>
public sealed class ResultPlanRoot<TMessage, TResult, THandler> : ResultPlanRoot
    where TMessage : notnull, IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{
    private readonly Func<THandler>? _directHandlerFactory;

    /// <summary>Creates a plan without a compile-time construction path.</summary>
    public ResultPlanRoot()
    {
    }

    internal ResultPlanRoot(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IResultPlanRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult, THandler>(state);
}

/// <summary>Generic re-entry point for consumers of <see cref="ResultPlanRoot"/>.</summary>
public interface IResultPlanRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message, result and handler types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult, THandler>(TState state)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>;
}
