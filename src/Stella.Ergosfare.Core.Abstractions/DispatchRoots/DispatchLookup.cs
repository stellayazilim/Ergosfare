using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// Closes a dispatch generic over a message's runtime type, preferring the generated root
/// for the type and falling back to reflection for types the generator never saw.
/// </summary>
/// <remarks>
/// A type with a generated root is closed from inside a generic context, so nothing is
/// constructed reflectively. Without one — a message registered only at runtime — the
/// closed type is built with <see cref="Type.MakeGenericType"/>, which a trimmed or
/// AOT-published application cannot rely on. Those builds reach the fallback only for
/// constructs they already cannot serve.
/// </remarks>
internal static class DispatchLookup
{
    /// <summary>
    /// Closes a generic over the message type alone — the shape void executors and
    /// broadcast invokers use.
    /// </summary>
    /// <typeparam name="TReturn">What the caller wants built.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="messageType">The message's runtime type.</param>
    /// <param name="visitor">Re-entered inside the generic context when a root exists.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <param name="unrootedDefinition">
    /// The open generic definition to close reflectively when no root exists.
    /// </param>
    /// <param name="unrootedArguments">Constructor arguments for that construction.</param>
    /// <returns>The closed instance.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for runtime-only registrations.")]
    internal static TReturn OverMessage<TReturn, TState>(
        Type messageType,
        IMessageRootVisitor<TReturn, TState> visitor,
        TState state,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type unrootedDefinition,
        object?[]? unrootedArguments = null)
        => GeneratedDispatchRoots.FindMessage(messageType) is { } root
            ? root.Accept(visitor, state)
            : (TReturn)Activator.CreateInstance(
                unrootedDefinition.MakeGenericType(messageType), unrootedArguments)!;

    /// <summary>
    /// Closes a generic over a (message, result) pair — the shape result executors use.
    /// </summary>
    /// <typeparam name="TReturn">What the caller wants built.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="messageType">The message's runtime type.</param>
    /// <param name="resultType">The result type.</param>
    /// <param name="visitor">Re-entered inside the generic context when a root exists.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <param name="unrootedDefinition">
    /// The open generic definition to close reflectively when no root exists.
    /// </param>
    /// <param name="unrootedArguments">Constructor arguments for that construction.</param>
    /// <returns>The closed instance.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The generic is closed over a live message's runtime type; the message roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for runtime-only registrations.")]
    internal static TReturn OverResult<TReturn, TState>(
        Type messageType,
        Type resultType,
        IMessageResultRootVisitor<TReturn, TState> visitor,
        TState state,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type unrootedDefinition,
        object?[]? unrootedArguments = null)
        => GeneratedDispatchRoots.FindResult(messageType, resultType) is { } root
            ? root.Accept(visitor, state)
            : (TReturn)Activator.CreateInstance(
                unrootedDefinition.MakeGenericType(messageType, resultType), unrootedArguments)!;

    /// <summary>
    /// Closes a generic over a (query, result) pair against the streaming roots.
    /// </summary>
    /// <typeparam name="TReturn">What the caller wants built.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="messageType">The query's runtime type.</param>
    /// <param name="resultType">The streamed item type.</param>
    /// <param name="visitor">Re-entered inside the generic context when a root exists.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <param name="unrootedDefinition">
    /// The open generic definition to close reflectively when no root exists.
    /// </param>
    /// <param name="unrootedArguments">Constructor arguments for that construction.</param>
    /// <returns>The closed instance.</returns>
    /// <remarks>
    /// Streaming roots are a separate store from
    /// <see cref="OverResult{TReturn,TState}"/>'s: one message can carry both a value
    /// contract and a stream contract for the same result type, and the two are not
    /// interchangeable.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The generic is closed over a live query's runtime type; the query roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Generated dispatch roots cover source-generated types; this reflective path is the JIT " +
                        "fallback for runtime-only registrations.")]
    internal static TReturn OverStream<TReturn, TState>(
        Type messageType,
        Type resultType,
        IMessageResultRootVisitor<TReturn, TState> visitor,
        TState state,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type unrootedDefinition,
        object?[]? unrootedArguments = null)
        => GeneratedDispatchRoots.FindStream(messageType, resultType) is { } root
            ? root.Accept(visitor, state)
            : (TReturn)Activator.CreateInstance(
                unrootedDefinition.MakeGenericType(messageType, resultType), unrootedArguments)!;
}
