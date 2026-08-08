using System;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>The concrete closure of <see cref="ResultPlanRoot"/>; instantiated by generated code.</summary>
public sealed class ResultPlanRoot<TMessage, TResult, THandler> : ResultPlanRoot
    where TMessage : notnull, IMessage
    where THandler : class, IAsyncHandler<TMessage, TResult>
{
    private readonly object? _directHandlerFactory;

    /// <summary>Creates a plan without a compile-time construction path.</summary>
    public ResultPlanRoot()
    {
    }

    internal ResultPlanRoot(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal ResultPlanRoot(Func<IServiceProvider, THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IResultPlanRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult, THandler>(state);
}
