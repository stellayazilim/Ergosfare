namespace Stella.Ergosfare.Core.Abstractions.StagedPlans;

/// <summary>
/// Receives a <see cref="StagedVoidPlan"/>'s message type as a generic argument.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IStagedVoidPlanVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Builds the result inside a generic context carrying the plan's message type.
    /// </summary>
    /// <typeparam name="TMessage">The plan's message type.</typeparam>
    /// <param name="state">The state passed to <see cref="StagedVoidPlan.Accept{TReturn, TState}"/>.</param>
    /// <returns>The visitor's result.</returns>
    TReturn Visit<TMessage>(TState state) where TMessage : IMessage;
}
