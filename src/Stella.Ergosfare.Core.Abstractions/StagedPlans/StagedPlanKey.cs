
namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// The pipeline a staged plan was compiled against: its main handlers and its four
/// interceptor stages, each as an ordered list of types in the order the pipeline would run
/// them.
/// </summary>
/// <remarks>
/// <para>
/// Registration is compared with this descriptor when the engine is initialized. Runtime
/// dispatch never rebuilds a pipeline; a mismatch is rejected.
/// </para>
/// <para>
/// The arrays are kept as given rather than copied: a plan is a compile-time singleton
/// whose pipeline never changes. Handlers are a list because a broadcast runs all of them;
/// commands and queries keep both segments as metadata while executing the winning
/// handler according to direct-then-covariant priority.
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

    /// <summary>
    /// Initializes the key of a single-handler pipeline: one handler registered for the
    /// message type itself, and none registered for a base type.
    /// </summary>
    /// <param name="handlerType">The pipeline's only main handler.</param>
    /// <param name="preInterceptorTypes">The pre-interceptors, in execution order.</param>
    /// <param name="postInterceptorTypes">The post-interceptors, in execution order.</param>
    /// <param name="exceptionInterceptorTypes">The exception interceptors, in execution order.</param>
    /// <param name="finalInterceptorTypes">The final interceptors, in execution order.</param>
    /// <param name="resultAdapterType">The result adapter the plan assumed, if any.</param>
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
    /// Initializes the key of any pipeline, carrying both handler segments.
    /// </summary>
    /// <param name="handlerTypes">
    /// The handlers registered for the message type itself, in execution order.
    /// </param>
    /// <param name="indirectHandlerTypes">
    /// The handlers registered for a base type, in execution order.
    /// </param>
    /// <param name="preInterceptorTypes">The pre-interceptors, in execution order.</param>
    /// <param name="postInterceptorTypes">The post-interceptors, in execution order.</param>
    /// <param name="exceptionInterceptorTypes">The exception interceptors, in execution order.</param>
    /// <param name="finalInterceptorTypes">The final interceptors, in execution order.</param>
    /// <param name="resultAdapterType">The result adapter the plan assumed, if any.</param>
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
    /// The pipeline's only main handler, or <c>null</c> when the plan was compiled against
    /// a list of them — a broadcast, where there is no single handler.
    /// </summary>
    public Type? HandlerType
        => HandlerTypeArray.Length == 1 && IndirectHandlerTypeArray.Length == 0
            ? HandlerTypeArray[0]
            : null;

    /// <summary>
    /// The result adapter the plan's value-path branches were compiled against, or
    /// <c>null</c> when the plan assumed none.
    /// </summary>
    /// <remarks>
    /// Describes the adapter embedded in the generated body. Adapter selection and
    /// validation happen at compile time; dispatch does not resolve an adapter service.
    /// </remarks>
    public Type? ResultAdapterType { get; }

    /// <summary>
    /// The main handlers registered for the message type itself, in execution order.
    /// </summary>
    public IReadOnlyList<Type> HandlerTypes => HandlerTypeArray;

    /// <summary>
    /// The main handlers registered for a base type of the message, in execution order.
    /// Commands and queries execute this segment only when no direct handler wins.
    /// </summary>
    public IReadOnlyList<Type> IndirectHandlerTypes => IndirectHandlerTypeArray;

    /// <summary>
    /// The pre-interceptors, in execution order.
    /// </summary>
    public IReadOnlyList<Type> PreInterceptorTypes => PreInterceptorTypeArray;

    /// <summary>
    /// The post-interceptors, in execution order.
    /// </summary>
    public IReadOnlyList<Type> PostInterceptorTypes => PostInterceptorTypeArray;

    /// <summary>
    /// The exception interceptors, in execution order.
    /// </summary>
    public IReadOnlyList<Type> ExceptionInterceptorTypes => ExceptionInterceptorTypeArray;

    /// <summary>
    /// The final interceptors, in execution order.
    /// </summary>
    public IReadOnlyList<Type> FinalInterceptorTypes => FinalInterceptorTypeArray;
}
