using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The pipeline composition a staged plan was baked against: the sole main handler plus
/// the four interceptor stages as ordered type lists — exactly the merged
/// (direct-first, then indirect) order the runtime pipeline would execute them in.
/// </summary>
/// <remarks>
/// The composition is the advisory contract's comparison key: on every registry-version
/// rebuild the executor compares it against the live pipeline, and any difference —
/// a runtime-registered interceptor, a different handler, reordered stages — routes the
/// dispatch back through the runtime strategy. The arrays are captured as given (no
/// defensive copy); plans are compile-time singletons whose compositions never change.
/// </remarks>
public sealed class StagedPlanComposition(
    Type handlerType,
    Type[] preInterceptorTypes,
    Type[] postInterceptorTypes,
    Type[] exceptionInterceptorTypes,
    Type[] finalInterceptorTypes)
{
    internal readonly Type[] PreInterceptorTypeArray = preInterceptorTypes;
    internal readonly Type[] PostInterceptorTypeArray = postInterceptorTypes;
    internal readonly Type[] ExceptionInterceptorTypeArray = exceptionInterceptorTypes;
    internal readonly Type[] FinalInterceptorTypeArray = finalInterceptorTypes;

    /// <summary>The concrete type of the pipeline's sole main handler.</summary>
    public Type HandlerType { get; } = handlerType;

    /// <summary>The pre-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> PreInterceptorTypes => PreInterceptorTypeArray;

    /// <summary>The post-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> PostInterceptorTypes => PostInterceptorTypeArray;

    /// <summary>The exception-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> ExceptionInterceptorTypes => ExceptionInterceptorTypeArray;

    /// <summary>The final-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> FinalInterceptorTypes => FinalInterceptorTypeArray;
}

/// <summary>
/// A compile-time staged pipeline plan for a void message: bespoke code that runs the
/// message's entire interceptor-bearing pipeline — pre stages, handler, post stages, with
/// exception and final semantics — as straight-line typed calls instead of the runtime
/// strategy's generic machinery. See <see cref="MessageRoot"/> for the visitor re-entry
/// pattern.
/// </summary>
/// <remarks>
/// The plan is advisory: the hosting executor re-validates <see cref="Composition"/>
/// against the live registry on every version change and falls back to the runtime
/// strategy whenever the pipeline no longer matches, so a stale plan only loses its
/// speedup, never changes behavior. <see cref="StagedVoidPlan{TMessage}.Execute"/> must
/// resolve every participant from the provider it is handed — that is exactly what the
/// runtime handler references do outside memoized mode, which the executor's gate
/// excludes.
/// </remarks>
public abstract class StagedVoidPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanComposition Composition { get; }

    /// <summary>Invokes the visitor with this plan's message type as the generic argument.</summary>
    public abstract TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state);
}

/// <summary>The typed closure of <see cref="StagedVoidPlan"/>; subclassed by generated (or hand-written) plans.</summary>
public abstract class StagedVoidPlan<TMessage> : StagedVoidPlan
    where TMessage : notnull, IMessage
{
    /// <summary>
    /// Runs the baked pipeline for the message. Only invoked while the live pipeline
    /// matches <see cref="StagedVoidPlan.Composition"/>; participants resolve from
    /// <paramref name="serviceProvider"/> — the dispatching scope's provider.
    /// </summary>
    public abstract ValueTask Execute(TMessage message, IExecutionContext context, IServiceProvider serviceProvider);

    /// <inheritdoc />
    public sealed override TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);
}

/// <summary>Generic re-entry point for consumers of <see cref="StagedVoidPlan"/>.</summary>
public interface IStagedVoidPlanVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message type as the generic argument.</summary>
    TReturn Visit<TMessage>(TState state) where TMessage : notnull, IMessage;
}

/// <summary>
/// The result-producing counterpart of <see cref="StagedVoidPlan"/>: a compile-time
/// staged pipeline plan for a message with a result contract. The same advisory contract
/// applies — see <see cref="StagedVoidPlan"/>.
/// </summary>
public abstract class StagedResultPlan
{
    /// <summary>The pipeline composition the plan was baked against.</summary>
    public abstract StagedPlanComposition Composition { get; }

    /// <summary>Invokes the visitor with this plan's message and result types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state);
}

/// <summary>The typed closure of <see cref="StagedResultPlan"/>; subclassed by generated (or hand-written) plans.</summary>
public abstract class StagedResultPlan<TMessage, TResult> : StagedResultPlan
    where TMessage : notnull, IMessage
{
    /// <inheritdoc cref="StagedVoidPlan{TMessage}.Execute"/>
    public abstract ValueTask<TResult> Execute(TMessage message, IExecutionContext context, IServiceProvider serviceProvider);

    /// <inheritdoc />
    public sealed override TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);
}

/// <summary>Generic re-entry point for consumers of <see cref="StagedResultPlan"/>.</summary>
public interface IStagedResultPlanVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message and result types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult>(TState state) where TMessage : notnull, IMessage;
}
