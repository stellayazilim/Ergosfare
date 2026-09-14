using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles failures of type <typeparamref name="TException"/> raised while dispatching any
/// command — the shape a module-wide error policy takes.
/// </summary>
/// <typeparam name="TException">
/// The failure type this interceptor accepts. Matching follows <c>catch</c> semantics, so
/// derived types match too.
/// </typeparam>
/// <remarks>
/// Accepting every command means joining the exception stage of every command pipeline in
/// the module; the filter is what keeps it from handling failures it was not written for.
/// The failure arrives already typed, so no type test is needed in the body.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptorFor<TException> :
    ICommand, IAsyncExceptionInterceptor<ICommand>, IExceptionInterceptorFilter<TException>
    where TException : Exception
{
    /// <summary>
    /// Forwards the core contract to the typed method below.
    /// </summary>
    /// <param name="command">The command whose dispatch failed.</param>
    /// <param name="messageResult">The result produced so far, if any.</param>
    /// <param name="exception">The failure being handled.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the typed method returned.</returns>
    async ValueTask<object> IAsyncExceptionInterceptor<ICommand>.HandleAsync(
        ICommand command, object? messageResult, Exception exception, ErgosfareContext context)
        // The cast is safe: the stage only runs this interceptor once its filter accepted
        // the failure.
        => await HandleAsync(command, messageResult, (TException)exception, context);

    /// <summary>
    /// Handles <paramref name="exception"/> and produces the result to continue with.
    /// </summary>
    /// <param name="command">The command whose dispatch failed.</param>
    /// <param name="messageResult">The result produced before the failure, if any.</param>
    /// <param name="exception">The failure being handled, already typed.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>
    /// The result the rest of the pipeline receives, which must be of the pipeline's result
    /// type.
    /// </returns>
    ValueTask<object> HandleAsync(ICommand command, object? messageResult, TException exception, ErgosfareContext context);
}
