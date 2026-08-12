
namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

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
    Type[] finalInterceptorTypes,
    Type? resultAdapterType = null)
{
    internal readonly Type[] PreInterceptorTypeArray = preInterceptorTypes;
    internal readonly Type[] PostInterceptorTypeArray = postInterceptorTypes;
    internal readonly Type[] ExceptionInterceptorTypeArray = exceptionInterceptorTypes;
    internal readonly Type[] FinalInterceptorTypeArray = finalInterceptorTypes;

    /// <summary>The concrete type of the pipeline's sole main handler.</summary>
    public Type HandlerType { get; } = handlerType;

    /// <summary>
    /// The result-adapter type the plan's value-path branches were baked against, or
    /// <c>null</c> when the plan models no adapter. Part of the comparison key: the
    /// hosting executor only trusts the plan while the runtime-bound adapter of the
    /// (message, result) slot is exactly this type — a plan emitted before an annotation
    /// was added (or by an older generator) then falls back to the runtime strategy
    /// instead of silently skipping the value path.
    /// </summary>
    public Type? ResultAdapterType { get; } = resultAdapterType;

    /// <summary>The pre-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> PreInterceptorTypes => PreInterceptorTypeArray;

    /// <summary>The post-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> PostInterceptorTypes => PostInterceptorTypeArray;

    /// <summary>The exception-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> ExceptionInterceptorTypes => ExceptionInterceptorTypeArray;

    /// <summary>The final-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> FinalInterceptorTypes => FinalInterceptorTypeArray;
}
