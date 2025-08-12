using Ergosfare.Core.Context;
using ExecutionAbortedException = Ergosfare.Core.Abstractions.Exceptions.ExecutionAbortedException;

namespace Ergosfare.Core.Internal.Contexts;


/// <summary>
/// <inheritdoc cref="Context.IExecutionContext"/>
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
    /// Sets <see cref="MessageResult"/> and throws <exception cref="Core.Abstractions.Exceptions.ExecutionAbortedException"></exception>
    /// </summary>
    /// <param name="messageResult"></param>
    public void Abort(object? messageResult = null)
    {
        MessageResult = messageResult;
        throw new ExecutionAbortedException();
        
    }
}