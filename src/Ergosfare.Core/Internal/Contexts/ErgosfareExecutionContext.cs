using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Internal.Contexts;


/// <summary>
/// <inheritdoc cref="IExecutionContext"/>
/// </summary>
internal sealed class ErgosfareExecutionContext(
    List<IPipelineCheckpoint>? checkpoints, 
    object message, 
    IDictionary<object, object?>? items, CancellationToken cancellationToken)
    : IExecutionContext
{


    /// <summary>
    /// Tracks the number of times <see cref="Retry"/> has been invoked
    /// for this execution context. Incremented each time a retry is requested.
    /// </summary>
    private byte _retryCounter = 0;
    
    
    /// <summary>
    /// Gets the total number of retries that have been requested so far
    /// for this execution context.
    /// </summary>
    public byte RetryCount => _retryCounter;
    
    /// <summary>
    /// Gets the <see cref="CancellationToken"/> associated with the current execution context.
    /// This token can be used to observe cancellation requests and propagate them to handlers or interceptors.
    /// </summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;
    
    
    /// <summary>
    /// Gets a dictionary of arbitrary key-value pairs stored in the execution context.
    /// This can be used to share data between different handlers, interceptors, or other pipeline components.
    /// </summary>
    public IDictionary<object, object?> Items { get; } = items ?? new Dictionary<object, object?>();

    public Type? CurrentHandlerType { get; set; } 
    
    /// <summary>
    /// Gets or sets the result produced by the message or query being processed in this execution context.
    /// For events, this may be <c>null</c>, while for commands or queries it may hold the returned value.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// The message being processed in the current execution context.
    /// </summary>
    public object Message { get; set;  } = message;

    /// <summary>
    /// The current execution status of the pipeline.
    /// Defaults to <see cref="PipelineStatus.Begin"/>.
    /// </summary>
    public PipelineStatus Status { get; set; } = PipelineStatus.Begin;

    /// <summary>
    /// The collection of checkpoints of the pipeline, if any.
    /// </summary>
    public List<IPipelineCheckpoint> Checkpoints { get;  } = checkpoints;

    /// <summary>
    /// Publishes a <see cref="PipelineRetrySignal"/> containing the current 
    /// <c>message</c>, <c>Result</c>, and <c>checkpoints</c>.
    /// Consumers subscribed to this signal can decide how to restart the 
    /// pipeline, replay steps, or resume execution from captured checkpoints.
    /// </summary>
    public void Retry()
    {
        // create retry signal
        var signal = new PipelineRetrySignal(message, Result, checkpoints);

        // emit the signal
        SignalHubAccessor.Instance.Publish(signal);

        throw new ExecutionRetryRequestedException(_retryCounter++);

    }

    /// <summary>
    /// Indicates whether the current step or checkpoint can be retried.
    /// </summary>
    public bool CanRetry => Checkpoints.Count > 0 && Status == PipelineStatus.Failed;

    /// <summary>
    /// Indicates whether the pipeline can continue execution from the current paused state.
    /// </summary>
    public bool CanContinue => Checkpoints.Count > 0 && Status == PipelineStatus.Paused;

    /// <summary>
    /// Continues execution of the pipeline from the current paused state.
    /// Throws <see cref="NotImplementedException"/> until implemented.
    /// </summary>
    public void Continue()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Pauses execution of the pipeline at the current point.
    /// Throws <see cref="NotImplementedException"/> until implemented.
    /// </summary>
    public void Pause()
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Stores an item in the execution context under the specified key.
    /// If an item with the same key already exists, it will be overwritten.
    /// </summary>
    /// <param name="key">The unique key to associate with the item.</param>
    /// <param name="item">The object to store in the context.</param>
    public void Set(string key, object item)
    {
        Items[key] = item;
    }

    
    /// <summary>
    /// Checks whether an item with the specified key exists in the context.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <returns><c>true</c> if an item with the given key exists; otherwise, <c>false</c>.</returns>
    public bool Has(string key)
    {
        return Items.ContainsKey(key);
    }

    
    /// <summary>
    /// Attempts to retrieve an item of the specified type from the context using the given key.
    /// </summary>
    /// <typeparam name="TType">The type of the item expected.</typeparam>
    /// <param name="key">The key associated with the item.</param>
    /// <param name="item">
    /// When this method returns, contains the retrieved item if found and of the correct type; otherwise, the default value for <typeparamref name="TType"/>.
    /// </param>
    /// <returns><c>true</c> if an item with the given key exists and is of the correct type; otherwise, <c>false</c>.</returns>
    public TType Get<TType>(string key) where TType : notnull
    {
        if (!Items.TryGetValue(key, out var ıtem)) 
            throw new InvalidOperationException("Item does not exist");
        return (TType)ıtem!;
    }


    /// <summary>
    /// Retrieves an item of the specified type from the context using the given key.
    /// </summary>
    /// <typeparam name="TType">The type of the item to retrieve.</typeparam>
    /// <param name="key">The key associated with the item.</param>
    /// <param name="item">the item to retrieve</param>
    /// <returns>The item associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no item exists with the specified key.</exception>
    /// <exception cref="InvalidCastException">Thrown if the stored item cannot be cast to <typeparamref name="TType"/>.</exception>
    public bool TryGet<TType>(string key, out TType item)
    {
        if (Items.TryGetValue(key, out var el))
        {
            item = (TType)el!;
            return true;
        }
        
        item = default!;
        return false;
    }

    
    /// <summary>
    /// Sets <see cref="MessageResult"/> and throws <exception cref="ExecutionAbortedException"></exception>
    /// </summary>
    /// <param name="messageResult"></param>
    public void Abort(object? messageResult = null)
    {
        Result = messageResult;
        throw new ExecutionAbortedException();
        
    }
    
    
}