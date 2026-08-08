namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>Generic re-entry point for consumers of <see cref="StagedResultPlan"/>.</summary>
public interface IStagedResultPlanVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message and result types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult>(TState state) where TMessage : IMessage;
}
