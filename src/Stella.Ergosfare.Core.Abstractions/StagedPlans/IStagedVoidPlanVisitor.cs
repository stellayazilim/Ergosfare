namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>Generic re-entry point for consumers of <see cref="StagedVoidPlan"/>.</summary>
public interface IStagedVoidPlanVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message type as the generic argument.</summary>
    TReturn Visit<TMessage>(TState state) where TMessage : notnull, IMessage;
}
