namespace Ergosfare.Core.Context;
/// <summary>
/// <inheritdoc cref="IExecutionContext"/>
/// </summary>
internal sealed class ExecutionContext: IExecutionContext
{

    public CancellationToken CancellationToken { get; } 
    public IDictionary<object, object?> Items { get; }
    public object? MessageResult { get; set; }

    public ExecutionContext(CancellationToken cancelationToken, IDictionary<object, object?> items)
    {
        CancellationToken = cancelationToken;
        Items = items;
    }
    
    /// <summary>
    /// Sets <see cref="MessageResult"/> and throws <exception cref="ExecutionAbortedException"></exception>
    /// </summary>
    /// <param name="messageResult"></param>
    public void Abort(object? messageResult = null)
    {
        MessageResult = messageResult;
        throw new ExecutionAbortedException();
        
    }
}