using System;
using System.Collections.Concurrent;

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
