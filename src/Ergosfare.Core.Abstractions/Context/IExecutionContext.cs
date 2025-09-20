using System;
using System.Collections.Generic;
using System.Threading;

namespace Ergosfare.Core.Abstractions;

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
    /// <param name="item">When this method returns, contains the retrieved item if found; otherwise, the default value of <typeparamref name="TType"/>.</param>
    /// <returns><c>true</c> if the item was found and is of type <typeparamref name="TType"/>; otherwise, <c>false</c>.</returns>
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
    ///     Gets the cancellation token associated with the execution context.
    /// </summary>
    /// <remarks>
    ///     This token can be used to cancel the execution of the current operation.
    ///     Handlers should periodically check this token and abort their execution if cancellation is requested.
    /// </remarks>
    CancellationToken CancellationToken { get; }
    

    /// <summary>
    ///     Gets a key/value collection that can be used to share data within the scope of this execution.
    /// </summary>
    /// <remarks>
    ///     This collection allows handlers to share data with each other during the execution of a single
    ///     mediation operation. The data is scoped to the current execution and is not shared across
    ///     different mediation operations.
    /// </remarks>
    IDictionary<object, object?> Items { get; }
    
    
    /// <summary>
    ///     The result of the message mediation.
    /// </summary>
    /// <remarks>
    ///     This property can be set by handlers to provide a result for the mediation operation.
    ///     It is typically set by the main handler, but can also be set by pre-handlers or post-handlers
    ///     in certain scenarios, such as when aborting the execution.
    /// </remarks>
    object? MessageResult { get; set; }
    
    
    /// <summary>
    ///     Aborts the execution of the current mediation execution.
    /// </summary>
    /// <param name="messageResult">
    ///     The message result to set before aborting. This is required if the message has a specific
    ///     result type and the execution is aborted in the pre-handler phase.
    /// </param>
    /// <remarks>
    ///     This method allows handlers to abort the execution of the current mediation operation.
    ///     When called, the execution is immediately aborted, and no further handlers are executed.
    ///     If the message has a specific result type and the execution is aborted in the pre-handler phase,
    ///     a message result must be provided to satisfy the result type requirement.
    /// </remarks>
    void Abort(object? messageResult = null);
}