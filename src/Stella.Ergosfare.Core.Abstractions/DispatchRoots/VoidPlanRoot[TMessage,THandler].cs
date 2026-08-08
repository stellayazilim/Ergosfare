using System;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>The concrete closure of <see cref="VoidPlanRoot"/>; instantiated by generated code.</summary>
public sealed class VoidPlanRoot<TMessage, THandler> : VoidPlanRoot
    where TMessage : notnull, IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private readonly object? _directHandlerFactory;

    /// <summary>Creates a plan without a compile-time construction path.</summary>
    public VoidPlanRoot()
    {
    }

    internal VoidPlanRoot(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal VoidPlanRoot(Func<IServiceProvider, THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IVoidPlanRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, THandler>(state);
}
