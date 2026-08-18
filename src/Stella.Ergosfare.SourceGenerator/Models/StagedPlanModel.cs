using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A plan for a message whose whole pipeline — its main handlers plus at least one
/// interceptor stage or plugin call — could be modelled exactly. A streaming pair gets a
/// plan even bare: streaming has no single-handler family to fall back on, so the plain
/// enumeration is the plan.
/// </summary>
/// <param name="MessageTypeExpression">The message this plan serves.</param>
/// <param name="Groups">The group set this plan was compiled for, empty for the default one.</param>
/// <param name="IsBroadcast">Whether the plan delivers to every handler rather than one.</param>
/// <param name="IsStream">
/// Whether the plan streams its results. The result type is then the streamed item type,
/// and the stages around the enumeration carry the enumerator rather than a result value.
/// </param>
/// <param name="ResultTypeExpression">The result type, or <c>null</c> when there is none.</param>
/// <param name="ResultIsValueType">Whether that result is a value type.</param>
/// <param name="Handlers">Main handlers registered for the message type itself.</param>
/// <param name="IndirectHandlers">Main handlers registered for a base type.</param>
/// <param name="PreCalls">The pre-interceptor stage.</param>
/// <param name="PostCalls">The post-interceptor stage.</param>
/// <param name="ExceptionCalls">The exception-interceptor stage.</param>
/// <param name="FinalCalls">The final-interceptor stage.</param>
/// <param name="AdapterKind">Which kind of result adapter the plan was compiled against.</param>
/// <param name="ResultAdapterTypeExpression">That adapter's type, when it has one.</param>
/// <param name="ResultAdapterMaterializes">Whether the adapter can also build a failed result.</param>
/// <param name="PluginCalls">The plugin methods called from inside this plan.</param>
/// <param name="GroupGuards">The group tests a filtering plan computes once at the top.</param>
/// <remarks>
/// <para>
/// Every stage is in invocation order — participants registered for the message type first,
/// then those registered for a base type, each by descending weight and then type name — and
/// the contract each call goes through was chosen at compile time. Like every plan it is a
/// proposal: the executor checks it against the composition the container selected and falls
/// back to the general strategy if they differ.
/// </para>
/// <para>
/// Handlers are two segments because a broadcast runs all of them. A command or query plan
/// is the same shape with a single direct handler, which is what lets one emission path and
/// one runtime check serve both: it carries its covariant segment for the check, and
/// delivers only to the direct handler.
/// </para>
/// </remarks>
internal sealed record StagedPlanModel(
    string MessageTypeExpression,
    ImmutableArray<string> Groups,
    bool IsBroadcast,
    bool IsStream,
    string? ResultTypeExpression,
    bool ResultIsValueType,
    ImmutableArray<StagedHandlerModel> Handlers,
    ImmutableArray<StagedHandlerModel> IndirectHandlers,
    ImmutableArray<StagedCallModel> PreCalls,
    ImmutableArray<StagedCallModel> PostCalls,
    ImmutableArray<StagedCallModel> ExceptionCalls,
    ImmutableArray<StagedCallModel> FinalCalls,
    StagedResultAdapterKind AdapterKind,
    string? ResultAdapterTypeExpression,
    bool ResultAdapterMaterializes,
    ImmutableArray<PluginInvocationModel> PluginCalls,
    ImmutableArray<StagedGroupGuardModel> GroupGuards)
{
    /// <summary>
    /// Whether this is the plan that serves dispatches whose groups are only known at
    /// runtime: it holds every participant and guards each call, so one body answers any
    /// set. A plan compiled for a known set has no guards, because participation was already
    /// decided.
    /// </summary>
    public bool IsGroupFiltering => !GroupGuards.IsEmpty;

    /// <summary>
    /// The type the interceptor stages carry in their result slot: the enumerator for a
    /// streaming plan, the declared result otherwise, <c>Unit</c> for a void pipeline.
    /// </summary>
    /// <remarks>
    /// A streaming pipeline's post, exception and final stages receive the enumerator
    /// rather than a result value — the items are already with the caller — so the stage
    /// contracts close over it instead of the item type.
    /// </remarks>
    public string PipelineResultTypeExpression => IsStream
        ? EmittedExpressions.AsyncEnumerator + "<" + ResultTypeExpression + ">"
        : ResultTypeExpression ?? EmittedExpressions.Unit;

    /// <summary>
    /// Whether the stage-carried result is a value type; see
    /// <see cref="PipelineResultTypeExpression"/>. An enumerator is a reference type
    /// whatever the item type is.
    /// </summary>
    public bool PipelineResultIsValueType => !IsStream && ResultTypeExpression is not null && ResultIsValueType;

    /// <summary>
    /// The one main handler's type. Meaningful only when the plan is not a broadcast.
    /// </summary>
    public string HandlerTypeExpression => Handlers[0].TypeExpression;

    /// <summary>
    /// How to construct that handler without the container, when it qualifies; see
    /// <see cref="HandlerTypeExpression"/>.
    /// </summary>
    public string? HandlerConstructionExpression => Handlers[0].ConstructionExpression;

    /// <summary>
    /// Reports whether any plugin method is called at a given point of this plan.
    /// </summary>
    /// <param name="hook">The point to ask about.</param>
    /// <returns><c>true</c> when at least one call is written there.</returns>
    /// <remarks>
    /// Every point is on the straight-line path, so this only ever decides whether a line is
    /// written — never whether the plan grows a guard. A plugin cannot change the shape of
    /// the pipeline it observes.
    /// </remarks>
    public bool HasPluginCalls(PluginHook hook)
    {
        foreach (var call in PluginCalls)
        {
            if (call.Hook == hook)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether every participant can be constructed without the container, which is what
    /// makes the plan's direct-construction variant worth writing.
    /// </summary>
    public bool SupportsDirectConstruction
        => AllHandlersConstructible(Handlers)
           && AllHandlersConstructible(IndirectHandlers)
           && AllConstructible(PreCalls)
           && AllConstructible(PostCalls)
           && AllConstructible(ExceptionCalls)
           && AllConstructible(FinalCalls);

    /// <summary>
    /// Reports whether every handler in a segment carries a construction.
    /// </summary>
    /// <param name="handlers">The segment to check.</param>
    /// <returns><c>true</c> when all of them do.</returns>
    private static bool AllHandlersConstructible(ImmutableArray<StagedHandlerModel> handlers)
    {
        foreach (var handler in handlers)
        {
            if (handler.ConstructionExpression is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reports whether every interceptor in a stage carries a construction.
    /// </summary>
    /// <param name="calls">The stage to check.</param>
    /// <returns><c>true</c> when all of them do.</returns>
    private static bool AllConstructible(ImmutableArray<StagedCallModel> calls)
    {
        foreach (var call in calls)
        {
            if (call.ConstructionExpression is null)
            {
                return false;
            }
        }

        return true;
    }
}
