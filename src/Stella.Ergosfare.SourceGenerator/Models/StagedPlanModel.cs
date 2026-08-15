using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     One main handler of a staged plan, with the construction expression the
///     direct-construction variant uses when the participant qualifies.
/// </summary>
/// <remarks>
///     No pattern-match arm travels with a handler the way it does with an interceptor
///     (<see cref="StagedCallModel"/>): a plan is only computed when every handler is the
///     asynchronous contract, so the emitted call is always the same member on the concrete
///     type. Anything else disqualifies the plan and the runtime strategy serves the message.
/// </remarks>
internal readonly record struct StagedHandlerModel(
    string TypeExpression,
    string? ConstructionExpression,
    string? GroupGuard = null);

/// <summary>
///     One group test a filtering plan evaluates once at the top of its body: the local's
///     name and the call that fills it.
/// </summary>
internal readonly record struct StagedGroupGuardModel(string Name, string Expression);

/// <summary>
///     A staged pipeline plan ready for emission: a message whose whole discovered
///     pipeline — its main handlers plus at least one interceptor stage or plugin call —
///     could be modeled exactly, with every stage in the runtime shape-builder's execution
///     order (direct first, then indirect, each segment weight-descending then ordinal by
///     type name) and every call's pattern-match arm resolved at compile time. Advisory like
///     every plan: the hosting executor re-validates the baked composition per registry
///     version and falls back to the runtime strategy on any mismatch.
/// </summary>
/// <remarks>
///     Handlers are two segments because a broadcast serves all of them, directly registered
///     ones first and covariantly matched ones after. A command or query plan is the same
///     shape with one direct handler and an empty indirect segment, which is what lets one
///     emission path and one runtime gate serve both.
/// </remarks>
internal sealed record StagedPlanModel(
    string MessageTypeExpression,
    ImmutableArray<string> Groups,
    bool IsBroadcast,
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
    ///     Whether this is the plan that serves dispatches whose group filter is a runtime
    ///     value: every participant is present and each call carries a guard, so one body
    ///     answers any set. A plan keyed by a proven set carries no guards — participation
    ///     there is a compile-time fact.
    /// </summary>
    public bool IsGroupFiltering => !GroupGuards.IsEmpty;

    /// <summary>The sole main handler's type; meaningful only when the plan is not a broadcast.</summary>
    public string HandlerTypeExpression => Handlers[0].TypeExpression;

    /// <summary>The sole main handler's construction expression; see <see cref="HandlerTypeExpression"/>.</summary>
    public string? HandlerConstructionExpression => Handlers[0].ConstructionExpression;

    /// <summary>Whether any plugin method is emitted at the given hook of this plan.</summary>
    /// <remarks>
    ///     Every hook is a straight-line position, so this only ever decides whether a line
    ///     is written — never whether the plan grows a guard. A plugin cannot change the
    ///     shape of a pipeline it observes.
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
    ///     Whether every participant — each handler and each interceptor — carries a
    ///     construction expression, making the plan eligible for the emitted
    ///     direct-construction variant (<c>ExecuteDirect</c>).
    /// </summary>
    public bool SupportsDirectConstruction
        => AllHandlersConstructible(Handlers)
           && AllHandlersConstructible(IndirectHandlers)
           && AllConstructible(PreCalls)
           && AllConstructible(PostCalls)
           && AllConstructible(ExceptionCalls)
           && AllConstructible(FinalCalls);

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
