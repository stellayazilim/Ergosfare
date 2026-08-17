namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// Receives a <see cref="StagedResultPlan"/>'s message and result types as generic
/// arguments.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IStagedResultPlanVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Builds the result inside a generic context carrying the plan's message and result
    /// types.
    /// </summary>
    /// <typeparam name="TMessage">The plan's message type.</typeparam>
    /// <typeparam name="TResult">The plan's result type.</typeparam>
    /// <param name="state">The state passed to <see cref="StagedResultPlan.Accept{TReturn, TState}"/>.</param>
    /// <returns>The visitor's result.</returns>
    TReturn Visit<TMessage, TResult>(TState state) where TMessage : IMessage;
}
