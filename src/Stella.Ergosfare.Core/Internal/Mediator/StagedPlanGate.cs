using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Decides whether a compiled plan may be used: whether the live pipeline is exactly the
/// one the plan was compiled against.
/// </summary>
/// <remarks>
/// <para>
/// Handlers are compared the same way as the interceptor stages, which is what lets one
/// check serve a broadcast and a single-handler pipeline alike — the latter's compiled
/// pipeline is one direct handler and no indirect ones, so "exactly this one handler" falls
/// out of the same list comparison.
/// </para>
/// <para>
/// The indirect handlers are compared even for a single-handler plan, which would never run
/// them: a direct handler wins outright. That is on purpose. A covariant handler the
/// generator never saw means the live pipeline is not the compiled one, and a plan is
/// trusted only when the two match exactly. The dispatch then costs the general strategy
/// and behaves the same either way.
/// </para>
/// </remarks>
internal static class StagedPlanGate
{
    /// <summary>
    /// Reports whether the live participants are exactly what
    /// <paramref name="composition"/> was compiled against.
    /// </summary>
    /// <param name="dependencies">The participants this container resolved.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    /// <returns><c>true</c> when every stage matches, type for type and in order.</returns>
    internal static bool Matches(MessageDependencies dependencies, StagedPlanKey composition)
        => StageMatches(dependencies.Handlers, composition.HandlerTypeArray)
           && StageMatches(dependencies.IndirectHandlers, composition.IndirectHandlerTypeArray)
           && StageMatches(dependencies.PreInterceptors, composition.PreInterceptorTypeArray)
           && StageMatches(dependencies.PostInterceptors, composition.PostInterceptorTypeArray)
           && StageMatches(dependencies.ExceptionInterceptors, composition.ExceptionInterceptorTypeArray)
           && StageMatches(dependencies.FinalInterceptors, composition.FinalInterceptorTypeArray);

    /// <summary>
    /// Reports whether one live stage holds exactly the compiled types, in order.
    /// </summary>
    /// <typeparam name="THandler">The stage's participant contract.</typeparam>
    /// <param name="stage">The live stage.</param>
    /// <param name="baked">The types the plan was compiled with.</param>
    /// <returns><c>true</c> when both name the same types in the same order.</returns>
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
    /// Names the stages where the live pipeline differs from what
    /// <paramref name="composition"/> compiled, for the exception a mismatch raises.
    /// </summary>
    /// <param name="dependencies">The participants this container resolved.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    /// <returns>One clause per diverged stage, compiled types against live ones.</returns>
    internal static string DescribeMismatch(MessageDependencies dependencies, StagedPlanKey composition)
    {
        var clauses = new List<string>(2);

        AppendStageMismatch(clauses, "handlers", dependencies.Handlers, composition.HandlerTypeArray);
        AppendStageMismatch(clauses, "indirect handlers", dependencies.IndirectHandlers, composition.IndirectHandlerTypeArray);
        AppendStageMismatch(clauses, "pre-interceptors", dependencies.PreInterceptors, composition.PreInterceptorTypeArray);
        AppendStageMismatch(clauses, "post-interceptors", dependencies.PostInterceptors, composition.PostInterceptorTypeArray);
        AppendStageMismatch(clauses, "exception interceptors", dependencies.ExceptionInterceptors, composition.ExceptionInterceptorTypeArray);
        AppendStageMismatch(clauses, "final interceptors", dependencies.FinalInterceptors, composition.FinalInterceptorTypeArray);

        return string.Join("; ", clauses);
    }

    /// <summary>
    /// Adds one stage's clause to <paramref name="clauses"/> when it diverged.
    /// </summary>
    /// <typeparam name="THandler">The stage's participant contract.</typeparam>
    /// <param name="clauses">The clauses collected so far.</param>
    /// <param name="stageName">The stage's name in the exception message.</param>
    /// <param name="stage">The live stage.</param>
    /// <param name="baked">The types the plan was compiled with.</param>
    private static void AppendStageMismatch<THandler>(
        List<string> clauses,
        string stageName,
        IReadOnlyList<IHandlerReference<THandler>> stage,
        Type[] baked)
    {
        if (StageMatches(stage, baked))
        {
            return;
        }

        var live = stage.Count == 0 ? "none" : string.Join(", ", stage.Select(reference => reference.HandlerType.Name));
        var compiled = baked.Length == 0 ? "none" : string.Join(", ", baked.Select(type => type.Name));

        clauses.Add($"{stageName}: compiled [{compiled}], live [{live}]");
    }

    /// <summary>
    /// Reports whether every participant of <paramref name="composition"/> was registered
    /// in the module's own plain transient shape — the further condition a plan must meet
    /// before it may construct participants itself instead of resolving them.
    /// </summary>
    /// <param name="factory">The container's dependencies factory, which knows the registrations.</param>
    /// <param name="composition">The pipeline the plan was compiled against.</param>
    /// <returns><c>true</c> when constructing every participant is equivalent to resolving it.</returns>
    /// <remarks>
    /// Any override — a user's factory, a changed lifetime — sends the plan back to its
    /// resolving variant.
    /// </remarks>
    internal static bool AllPlainTransient(MessageDependenciesFactory factory, StagedPlanKey composition)
        => StagePlainTransient(factory, composition.HandlerTypeArray)
           && StagePlainTransient(factory, composition.IndirectHandlerTypeArray)
           && StagePlainTransient(factory, composition.PreInterceptorTypeArray)
           && StagePlainTransient(factory, composition.PostInterceptorTypeArray)
           && StagePlainTransient(factory, composition.ExceptionInterceptorTypeArray)
           && StagePlainTransient(factory, composition.FinalInterceptorTypeArray);

    /// <summary>
    /// Reports whether every type of one stage was registered in the plain transient shape.
    /// </summary>
    /// <param name="factory">The container's dependencies factory.</param>
    /// <param name="participants">The stage's participant types.</param>
    /// <returns><c>true</c> when all of them were.</returns>
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
