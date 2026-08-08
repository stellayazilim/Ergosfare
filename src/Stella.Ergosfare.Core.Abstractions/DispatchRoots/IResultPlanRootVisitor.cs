using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>Generic re-entry point for consumers of <see cref="ResultPlanRoot"/>.</summary>
public interface IResultPlanRootVisitor<out TReturn, in TState>
{
    /// <summary>Called with the plan's message, result and handler types as the generic arguments.</summary>
    TReturn Visit<TMessage, TResult, THandler>(TState state)
        where TMessage : notnull, IMessage
        where THandler : class, IAsyncHandler<TMessage, TResult>;
}
