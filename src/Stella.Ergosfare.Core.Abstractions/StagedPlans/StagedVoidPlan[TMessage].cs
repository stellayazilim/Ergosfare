using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>The typed closure of <see cref="StagedVoidPlan"/>; subclassed by generated (or hand-written) plans.</summary>
public abstract class StagedVoidPlan<TMessage> : StagedVoidPlan
    where TMessage : notnull, IMessage
{
    /// <summary>
    /// Runs the baked pipeline for the message. Only invoked while the live pipeline
    /// matches <see cref="StagedVoidPlan.Composition"/>; participants resolve from
    /// <paramref name="serviceProvider"/> — the dispatching scope's provider.
    /// </summary>
    public abstract ValueTask Execute(TMessage message, IExecutionContext context, IServiceProvider serviceProvider);

    /// <inheritdoc />
    public sealed override TReturn Accept<TReturn, TState>(IStagedVoidPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);
}
