
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>The concrete closure of <see cref="ResultHandlerPlan"/>; instantiated by generated code.</summary>
public sealed class ResultHandlerPlan<TMessage, TResult, THandler> : ResultHandlerPlan
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{
    private readonly object? _directHandlerFactory;

    /// <summary>Creates a plan without a compile-time construction path.</summary>
    public ResultHandlerPlan()
    {
    }

    internal ResultHandlerPlan(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal ResultHandlerPlan(Func<IServiceProvider, THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IResultHandlerPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult, THandler>(state);
}
