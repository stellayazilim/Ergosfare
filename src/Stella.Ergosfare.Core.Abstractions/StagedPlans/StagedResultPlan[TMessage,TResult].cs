

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;
/// <summary>The typed closure of <see cref="StagedResultPlan"/>; subclassed by generated (or hand-written) plans.</summary>
public abstract class StagedResultPlan<TMessage, TResult> : StagedResultPlan
    where TMessage : notnull, IMessage
{
    /// <inheritdoc cref="StagedVoidPlan{TMessage}.Execute"/>
    public abstract ValueTask<TResult> Execute(TMessage message, IExecutionContext context, IServiceProvider serviceProvider);

    /// <inheritdoc />
    public sealed override TReturn Accept<TReturn, TState>(IStagedResultPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);
}
