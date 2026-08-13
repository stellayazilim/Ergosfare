
namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// The pipeline composition a staged plan was baked against: the main handlers plus the four
/// interceptor stages as ordered type lists — exactly the merged (direct-first, then
/// indirect) order the runtime pipeline would execute them in.
/// </summary>
/// <remarks>
/// <para>
/// The composition is the advisory contract's comparison key: on every registry-version
/// rebuild the executor compares it against the live pipeline, and any difference —
/// a runtime-registered interceptor, a different handler, reordered stages — routes the
/// dispatch back through the runtime strategy. The arrays are captured as given (no
/// defensive copy); plans are compile-time singletons whose compositions never change.
/// </para>
/// <para>
/// Handlers are a list because a broadcast serves all of them. A single-handler pipeline —
/// every command and query plan — is the same shape with one direct handler and an empty
/// indirect segment, so the comparison has one implementation rather than one per family.
/// </para>
/// </remarks>
public sealed class StagedPlanKey
{
    internal readonly Type[] HandlerTypeArray;
    internal readonly Type[] IndirectHandlerTypeArray;
    internal readonly Type[] PreInterceptorTypeArray;
    internal readonly Type[] PostInterceptorTypeArray;
    internal readonly Type[] ExceptionInterceptorTypeArray;
    internal readonly Type[] FinalInterceptorTypeArray;

    /// <summary>The single-handler composition: one directly registered handler, no covariant ones.</summary>
    public StagedPlanKey(
        Type handlerType,
        Type[] preInterceptorTypes,
        Type[] postInterceptorTypes,
        Type[] exceptionInterceptorTypes,
        Type[] finalInterceptorTypes,
        Type? resultAdapterType = null)
        : this(
            [handlerType],
            [],
            preInterceptorTypes,
            postInterceptorTypes,
            exceptionInterceptorTypes,
            finalInterceptorTypes,
            resultAdapterType)
    {
    }

    /// <summary>
    /// The general composition, carrying both handler segments. The split mirrors the
    /// runtime's own: directly registered handlers first, then the covariantly matched ones,
    /// each segment in its baked execution order.
    /// </summary>
    public StagedPlanKey(
        Type[] handlerTypes,
        Type[] indirectHandlerTypes,
        Type[] preInterceptorTypes,
        Type[] postInterceptorTypes,
        Type[] exceptionInterceptorTypes,
        Type[] finalInterceptorTypes,
        Type? resultAdapterType = null)
    {
        HandlerTypeArray = handlerTypes;
        IndirectHandlerTypeArray = indirectHandlerTypes;
        PreInterceptorTypeArray = preInterceptorTypes;
        PostInterceptorTypeArray = postInterceptorTypes;
        ExceptionInterceptorTypeArray = exceptionInterceptorTypes;
        FinalInterceptorTypeArray = finalInterceptorTypes;
        ResultAdapterType = resultAdapterType;
    }

    /// <summary>
    /// The concrete type of the pipeline's sole main handler, or <c>null</c> when the plan
    /// was baked against a handler list — a broadcast, where "the" handler does not exist.
    /// </summary>
    public Type? HandlerType
        => HandlerTypeArray.Length == 1 && IndirectHandlerTypeArray.Length == 0
            ? HandlerTypeArray[0]
            : null;

    /// <summary>
    /// The result-adapter type the plan's value-path branches were baked against, or
    /// <c>null</c> when the plan models no adapter. Part of the comparison key: the
    /// hosting executor only trusts the plan while the runtime-bound adapter of the
    /// (message, result) slot is exactly this type — a plan emitted before an annotation
    /// was added (or by an older generator) then falls back to the runtime strategy
    /// instead of silently skipping the value path.
    /// </summary>
    public Type? ResultAdapterType { get; }

    /// <summary>The directly registered main handlers, in execution order.</summary>
    public IReadOnlyList<Type> HandlerTypes => HandlerTypeArray;

    /// <summary>
    /// The covariantly matched main handlers, in execution order — handlers registered
    /// against a base type or interface of the message. Empty for every single-handler
    /// pipeline: a covariant main handler disqualifies those plans outright.
    /// </summary>
    public IReadOnlyList<Type> IndirectHandlerTypes => IndirectHandlerTypeArray;

    /// <summary>The pre-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> PreInterceptorTypes => PreInterceptorTypeArray;

    /// <summary>The post-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> PostInterceptorTypes => PostInterceptorTypeArray;

    /// <summary>The exception-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> ExceptionInterceptorTypes => ExceptionInterceptorTypeArray;

    /// <summary>The final-interceptor types, in execution order.</summary>
    public IReadOnlyList<Type> FinalInterceptorTypes => FinalInterceptorTypeArray;
}
