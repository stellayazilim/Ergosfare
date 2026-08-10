
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
    /// Ends the current mediation: nothing after the calling participant runs, and the
    /// caller is told, by <see cref="Exceptions.ExecutionAbortedException"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dispatch was asked for by the call site, so the call site is who hears that it
    /// did not happen — a pipeline whose participants can abort is one the caller wraps in
    /// a <c>try</c>. The alternative, returning the result type's default, is
    /// indistinguishable from a handler that legitimately produced nothing.
    /// </para>
    /// <para>
    /// Stopping means stopping: nothing downstream runs. Not the rest of the current stage,
    /// not the exception stage — an abort is not a failure and exception interceptors exist
    /// to handle failures — and not the final stage either. There is no result to expect
    /// from a pipeline that was cut, which is why the signal carries none.
    /// </para>
    /// <para>
    /// The mechanism does not change with the shape of the pipeline: with interceptors or
    /// without, the signal travels straight out to the caller.
    /// </para>
    /// </remarks>
    /// <exception cref="Exceptions.ExecutionAbortedException">Always — this is how the abort travels.</exception>
    void Abort();

    /// <summary>
    /// Stops the pipeline, saying why. See <see cref="Abort()"/>.
    /// </summary>
    /// <param name="reason">
    /// Why the pipeline is being stopped; arrives on
    /// <see cref="Exceptions.ExecutionAbortedException.Reason"/>.
    /// </param>
    /// <exception cref="Exceptions.ExecutionAbortedException">Always — this is how the abort travels.</exception>
    void Abort(string? reason);

    /// <summary>
    /// Stops the pipeline, saying why and handing the caller something to act on. See
    /// <see cref="Abort()"/>.
    /// </summary>
    /// <param name="reason">
    /// Why the pipeline is being stopped; arrives on
    /// <see cref="Exceptions.ExecutionAbortedException.Reason"/>.
    /// </param>
    /// <param name="value">
    /// Data about the abort, arriving on
    /// <see cref="Exceptions.ExecutionAbortedException.Value"/> — a validation failure, a
    /// policy decision, whatever the caller needs. It is not the pipeline's result; a
    /// stopped pipeline has none.
    /// </param>
    /// <exception cref="Exceptions.ExecutionAbortedException">Always — this is how the abort travels.</exception>
    void Abort(string? reason, object? value);
}