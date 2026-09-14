namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>The immutable descriptor carried by an executable generated plan.</summary>
public interface ICompiledPlan
{
    /// <summary>The participants and adapter baked into the execution body.</summary>
    StagedPlanKey Composition { get; }

    /// <summary>The groups covered by a filtering body, or null for a fixed body.</summary>
    string[]? FilterGroups => null;

}

/// <summary>An executable generated streaming plan, without a runtime dispatch wrapper.</summary>
/// <typeparam name="TResult">The streamed item type.</typeparam>
public interface ICompiledStreamPlan<TResult> : ICompiledPlan
{
    /// <summary>Runs the generated iterator against the caller's scope.</summary>
    IAsyncEnumerable<TResult> Execute(object message, ErgosfareContext context,
        IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
