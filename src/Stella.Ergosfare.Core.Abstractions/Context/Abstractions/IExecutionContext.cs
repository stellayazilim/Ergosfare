
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Represents the execution context for message handling and mediation.
/// Provides access to contextual information such as cancellation tokens, items, pipeline state,
/// and control over pipeline execution (retry, pause, continue, abort).
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Stores an item in the execution context associated with the specified key.
    /// </summary>
    /// <param name="key">The key used to identify the item.</param>
    /// <param name="item">The object to store in the context.</param>
    void Set(string key, object item);

    /// <summary>
    /// Determines whether an item with the specified key exists in the execution context.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <returns><c>true</c> if an item with the key exists; otherwise, <c>false</c>.</returns>
    bool Has(string key);

    /// <summary>
    /// Tries to retrieve an item of type <typeparamref name="TType"/> from the execution context.
    /// </summary>
    /// <typeparam name="TType">The expected type of the item.</typeparam>
    /// <param name="key">The key associated with the item.</param>
    /// <param name="item">
    /// When this method returns, contains the retrieved item if found; otherwise, the default value of <typeparamref name="TType"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the item was found and is of type <typeparamref name="TType"/>; otherwise, <c>false</c>.
    /// </returns>
    bool TryGet<TType>(string key, out TType item);

    /// <summary>
    /// Retrieves an item of type <typeparamref name="TType"/> from the execution context.
    /// </summary>
    /// <typeparam name="TType">The expected type of the item.</typeparam>
    /// <param name="key">The key associated with the item.</param>
    /// <returns>The item associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no item exists with the specified key.</exception>
    /// <exception cref="InvalidCastException">Thrown if the item is not of type <typeparamref name="TType"/>.</exception>
    TType Get<TType>(string key) where TType : notnull;

    /// <summary>
    /// Gets the cancellation token associated with the execution context.
    /// Handlers should periodically check this token and abort execution if cancellation is requested.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets a key/value collection for sharing data within the scope of this execution.
    /// Data is scoped to the current execution and is not shared across different mediation operations.
    /// </summary>
    IDictionary<object, object?> Items { get; }

    /// <summary>
    /// Opens a child execution-context scope for a nested mediator call: the child starts
    /// with clean items and inherits this context's cancellation token. Dispose the scope
    /// when the nested call completes; the child must not be used afterwards.
    /// </summary>
    /// <example>
    /// <code>
    /// using var scope = context.CreateScope();
    /// await commandMediator.SendAsync(new InnerCommand(), scope.Context);
    /// </code>
    /// </example>
    ExecutionContextScope CreateScope();

    /// <summary>
    /// Short-circuits the current mediation: nothing after the calling participant runs,
    /// and the caller gets whatever the pipeline had produced by then.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aborting is not a failure. The caller sees no exception, the exception-interceptor
    /// stage does not run, and the remaining stages of the aborting participant's own stage
    /// are skipped. Final interceptors still run — they always do — and are handed the same
    /// result the caller receives, with no exception.
    /// </para>
    /// <para>
    /// What the caller receives is what the pipeline had already produced. Abort after the
    /// handler has run and its result is delivered; abort before it and there is nothing to
    /// deliver, so the caller gets <c>null</c> or the result type's default. A resultless
    /// dispatch simply completes.
    /// </para>
    /// </remarks>
    void Abort();
}