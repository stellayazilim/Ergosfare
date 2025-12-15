using System;
using System.Collections.Generic;
using System.Threading;

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
    /// Aborts the execution of the current mediation operation.
    /// </summary>
    /// <param name="messageResult">
    /// The message result to set before aborting. Required if the message has a specific result type
    /// and the execution is aborted in the pre-handler phase.
    /// </param>
    /// <remarks>
    /// When called, execution is immediately aborted and no further handlers are executed.
    /// If a message result is required, it must be provided to satisfy the result type requirement.
    /// </remarks>
    void Abort(object? messageResult = null);
}