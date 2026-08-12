using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>Generic re-entry point for consumers of <see cref="ResultHandlerPlan"/>.</summary>
public interface IResultHandlerPlanVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message, result and handler types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult, THandler>(TState state)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>;
}
