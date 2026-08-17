
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// <see cref="VoidHandlerPlan"/> closed over a concrete message and handler; instantiated
/// by generated registration code.
/// </summary>
/// <typeparam name="TMessage">The message this plan serves.</typeparam>
/// <typeparam name="THandler">The handler this plan invokes.</typeparam>
public sealed class VoidHandlerPlan<TMessage, THandler> : VoidHandlerPlan
    where TMessage : IMessage
    where THandler : class, IAsyncHandler<TMessage>
{
    private readonly object? _directHandlerFactory;

    /// <summary>
    /// Initializes a plan whose handler is obtained from the container.
    /// </summary>
    public VoidHandlerPlan()
    {
    }

    /// <summary>
    /// Initializes a plan that can construct the handler itself.
    /// </summary>
    /// <param name="directHandlerFactory">Constructs the handler.</param>
    internal VoidHandlerPlan(Func<THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    /// <summary>
    /// Initializes a plan that can construct the handler from a provider.
    /// </summary>
    /// <param name="directHandlerFactory">
    /// Constructs the handler, resolving its dependencies from the given provider.
    /// </param>
    internal VoidHandlerPlan(Func<IServiceProvider, THandler> directHandlerFactory)
        => _directHandlerFactory = directHandlerFactory;

    /// <summary>
    /// The construction path supplied at initialization, or <c>null</c>.
    /// </summary>
    internal override object? DirectHandlerFactory => _directHandlerFactory;

    /// <summary>
    /// Calls <paramref name="visitor"/> with <typeparamref name="TMessage"/> and
    /// <typeparamref name="THandler"/> as its generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public override TReturn Accept<TReturn, TState>(IVoidHandlerPlanVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, THandler>(state);
}
