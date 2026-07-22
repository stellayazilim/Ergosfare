using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions;


/// <summary>
/// Defines a mediator responsible for dispatching messages to their corresponding handlers
/// and returning results from the mediation process.
/// </summary>
public interface IMessageMediator
{
    /// <summary>
    /// Dispatches <paramref name="message"/> through the cached pipeline executor closed over
    /// its runtime type. The handler is invoked through its typed member — no object-typed
    /// bridge. Serves default-group dispatches; group-filtered dispatches go through
    /// <see cref="Mediate{TMessage, TMessageResult}"/>.
    /// </summary>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="items">Optional items exposed on the execution context.</param>
    /// <param name="cancellationToken">Cancellation token exposed on the execution context.</param>
    ValueTask DispatchAsync(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null);

    /// <summary>
    /// Dispatches <paramref name="message"/> through the cached result-producing pipeline
    /// executor closed over its runtime type; see <see cref="DispatchAsync"/>.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the pipeline.</typeparam>
    /// <param name="message">The message instance to dispatch.</param>
    /// <param name="items">Optional items exposed on the execution context.</param>
    /// <param name="cancellationToken">Cancellation token exposed on the execution context.</param>
    ValueTask<TResult> DispatchAsync<TResult>(object message, IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default, IEnumerable<string>? groups = null);

    
    /// <summary>
    /// Dispatches a message of type <typeparamref name="TMessage"/> to the appropriate handler(s)
    /// and returns the result of type <typeparamref name="TMessageResult"/>.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message being mediated. Must be non-nullable.</typeparam>
    /// <typeparam name="TMessageResult">The type of the result returned from the handler.</typeparam>
    /// <param name="message">The message instance to mediate.</param>
    /// <param name="options">The options controlling message resolution, dependency creation, and mediation strategy.</param>
    /// <returns>The result produced by the message handler(s).</returns>
    /// <remarks>
    /// Implementations are expected to handle the resolution of handlers for the specified message,
    /// create necessary dependencies, and execute the mediation strategy defined in <paramref name="options"/>.
    /// The mediation process may throw exceptions if no handlers are found or if there are issues
    /// resolving dependencies.
    /// </remarks>
    TMessageResult Mediate<TMessage, TMessageResult>(TMessage message, MediateOptions<TMessage, TMessageResult> options)
        where TMessage : notnull;
}