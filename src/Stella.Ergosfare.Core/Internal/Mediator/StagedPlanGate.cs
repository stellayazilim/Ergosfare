using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// The staged plans' advisory gate: whether the live pipeline is exactly the composition a
/// plan was baked against — one main handler of the planned type and the four interceptor
/// stages matching the planned type lists in order. Anything else (a runtime-registered
/// interceptor, a different or additional handler, reordered stages) fails the match and
/// keeps the dispatch on the runtime strategy.
/// </summary>
internal static class StagedPlanGate
{
    internal static bool Matches(MessageDependencies dependencies, StagedPlanKey composition)
        => dependencies.Handlers.Count == 1
           && dependencies.Handlers[0].HandlerType == composition.HandlerType
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
        => factory.IsPlainTransientRegistration(composition.HandlerType)
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
