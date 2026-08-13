using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>Generic re-entry point for consumers of <see cref="VoidHandlerPlan"/>.</summary>
public interface IVoidHandlerPlanVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message and handler types as the generic arguments.</summary>
    TReturn Visit<TMessage, THandler>(TState state)
        where TMessage : IMessage
        where THandler : class, IAsyncHandler<TMessage>;
}
