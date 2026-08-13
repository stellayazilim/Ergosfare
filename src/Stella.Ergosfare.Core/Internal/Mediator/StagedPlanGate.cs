using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The staged plans' advisory gate: whether the live pipeline is exactly the composition a
/// plan was baked against — the two main-handler segments and the four interceptor stages
/// matching the planned type lists in order. Anything else (a runtime-registered
/// interceptor, a different or additional handler, reordered stages) fails the match and
/// keeps the dispatch on the runtime strategy.
/// </summary>
/// <remarks>
/// <para>
/// Handlers are compared exactly like the interceptor stages, which is what lets one gate
/// serve both a broadcast and a single-handler pipeline: the latter's baked composition is
/// one direct handler and an empty indirect segment, so "the live pipeline has exactly this
/// one handler" falls out of the same list comparison instead of needing its own arm.
/// </para>
/// <para>
/// The indirect segment is compared even for a single-handler plan, which does not run it —
/// a sole direct handler wins the resolution ladder outright. That is deliberate: a covariant
/// handler the generator never saw means the live composition is not the one the plan was
/// baked against, and the advisory contract is "trust the plan only when the pipeline is
/// exactly what was compiled". The dispatch then costs the strategy instead of the plan, and
/// behaves identically either way.
/// </para>
/// </remarks>
internal static class StagedPlanGate
{
    internal static bool Matches(MessageDependencies dependencies, StagedPlanKey composition)
        => StageMatches(dependencies.Handlers, composition.HandlerTypeArray)
           && StageMatches(dependencies.IndirectHandlers, composition.IndirectHandlerTypeArray)
           && StageMatches(dependencies.PreInterceptors, composition.PreInterceptorTypeArray)
           && StageMatches(dependencies.PostInterceptors, composition.PostInterceptorTypeArray)
           && StageMatches(dependencies.ExceptionInterceptors, composition.ExceptionInterceptorTypeArray)
           && StageMatches(dependencies.FinalInterceptors, composition.FinalInterceptorTypeArray);

    private static bool StageMatches<THandler>(
        IReadOnlyList<IHandlerReference<THandler>> stage,
        Type[] baked)
    {
        if (stage.Count != baked.Length)
        {
            return false;
        }

        for (var i = 0; i < baked.Length; i++)
        {
            if (stage[i].HandlerType != baked[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The direct-construction gate on top of a composition match: every participant's
    /// effective DI registration must be the module's own plain transient one — the
    /// single shape where a plan constructing participants with <c>new</c> is observably
    /// identical to container resolution. Any override (user factory, lifetime change)
    /// routes the plan back to its provider-resolving variant.
    /// </summary>
    internal static bool AllPlainTransient(MessageDependenciesFactory factory, StagedPlanKey composition)
        => StagePlainTransient(factory, composition.HandlerTypeArray)
           && StagePlainTransient(factory, composition.IndirectHandlerTypeArray)
           && StagePlainTransient(factory, composition.PreInterceptorTypeArray)
           && StagePlainTransient(factory, composition.PostInterceptorTypeArray)
           && StagePlainTransient(factory, composition.ExceptionInterceptorTypeArray)
           && StagePlainTransient(factory, composition.FinalInterceptorTypeArray);

    private static bool StagePlainTransient(MessageDependenciesFactory factory, Type[] participants)
    {
        foreach (var participant in participants)
        {
            if (!factory.IsPlainTransientRegistration(participant))
            {
                return false;
            }
        }

        return true;
    }
}
