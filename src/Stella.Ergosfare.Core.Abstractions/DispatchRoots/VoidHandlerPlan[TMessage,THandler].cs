
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>The concrete closure of <see cref="VoidHandlerPlan"/>; instantiated by generated code.</summary>
public sealed class VoidHandlerPlan<TMessage, THandler> : VoidHandlerPlan
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private readonly object? _directHandlerFactory;

    /// <summary>Creates a plan without a compile-time construction path.</summary>
    public VoidHandlerPlan()
    {
    }

    internal VoidHandlerPlan(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal VoidHandlerPlan(Func<IServiceProvider, THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <inheritdoc />
    public override TReturn Accept<TReturn, TState>(IVoidHandlerPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, THandler>(state);
}
