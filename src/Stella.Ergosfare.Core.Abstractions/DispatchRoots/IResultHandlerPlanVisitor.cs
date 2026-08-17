using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// Receives a <see cref="ResultHandlerPlan"/>'s message, result and handler types as
/// generic arguments.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IResultHandlerPlanVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Builds the result inside a generic context carrying the plan's three types.
    /// </summary>
    /// <typeparam name="TMessage">The plan's message type.</typeparam>
    /// <typeparam name="TResult">The plan's result type.</typeparam>
    /// <typeparam name="THandler">The plan's handler type.</typeparam>
    /// <param name="state">The state passed to <see cref="ResultHandlerPlan.Accept{TReturn, TState}"/>.</param>
    /// <returns>The visitor's result.</returns>
    TReturn Visit<TMessage, TResult, THandler>(TState state)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>;
}
