using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// Receives a <see cref="VoidHandlerPlan"/>'s message and handler types as generic
/// arguments.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IVoidHandlerPlanVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Builds the result inside a generic context carrying the plan's message and handler
    /// types.
    /// </summary>
    /// <typeparam name="TMessage">The plan's message type.</typeparam>
    /// <typeparam name="THandler">The plan's handler type.</typeparam>
    /// <param name="state">The state passed to <see cref="VoidHandlerPlan.Accept{TReturn, TState}"/>.</param>
    /// <returns>The visitor's result.</returns>
    TReturn Visit<TMessage, THandler>(TState state)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage>;
}
