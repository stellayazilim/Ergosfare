using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

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
    internal static bool Matches(MessageDependencies dependencies, StagedPlanComposition composition)
        => dependencies.Handlers.Count == 1
           && dependencies.Handlers[0].HandlerType == composition.HandlerType
           && StageMatches(dependencies.PreInterceptors, composition.PreInterceptorTypeArray)
           && StageMatches(dependencies.PostInterceptors, composition.PostInterceptorTypeArray)
           && StageMatches(dependencies.ExceptionInterceptors, composition.ExceptionInterceptorTypeArray)
           && StageMatches(dependencies.FinalInterceptors, composition.FinalInterceptorTypeArray);

    private static bool StageMatches<THandler, TDescriptor>(
        IReadOnlyList<IHandlerReference<THandler, TDescriptor>> stage,
        Type[] baked)
        where TDescriptor : IHandlerDescriptor
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
}
