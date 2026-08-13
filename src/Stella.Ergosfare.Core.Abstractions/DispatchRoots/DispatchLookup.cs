using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;

/// <summary>
/// Closes a dispatch generic over a message's runtime type: the generated root first, so the
/// closure happens inside a generic context with no reflection at all, and a reflective
/// construction only for types the generator never saw.
/// </summary>
/// <remarks>
/// <para>
/// Every dispatch surface needs this and each used to spell it out again — the command and
/// query executor cache twice, the broadcast invoker cache, the stream invoker cache — each
/// with its own copy of the same trimming and AOT justifications. One policy stated four
/// times is four chances for them to drift apart, and the justification for reaching for
/// reflection at all belongs in one place.
/// </para>
/// <para>
/// The fallback is the JIT-only path: a runtime-registered message type has no generated
/// root, so the closed type is built with <see cref="Type.MakeGenericType"/>. A trimmed or
/// AOT-published application reaches it only for constructs the generator could not see,
/// which is the same set those builds already cannot serve.
/// </para>
/// </remarks>
internal static class DispatchLookup
{
    /// <summary>
    /// Closes a single-type dispatch generic (message only) — the shape the void executors
    /// and the broadcast invokers use.
    /// </summary>
    /// <param name="messageType">The message's runtime type.</param>
    /// <param name="visitor">Re-enters the generic context when the type has a generated root.</param>
    /// <param name="state">Carried into the visitor unchanged.</param>
    /// <param name="unrootedDefinition">Open generic definition to close reflectively when it does not.</param>
    /// <param name="unrootedArguments">Constructor arguments for that reflective construction.</param>
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
    /// Closes a (message, result) dispatch generic against the result roots — the shape the
    /// result executors use.
    /// </summary>
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
    /// Closes a (query, result) dispatch generic against the stream roots. Separate from
    /// <see cref="OverResult{TReturn,TState}"/> because streaming results are a different
    /// store: a message can carry both a value contract and a stream contract for the same
    /// result type, and they are not interchangeable.
    /// </summary>
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
