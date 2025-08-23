using Ergosfare.Context;

namespace Ergosfare.Core.Internal.Contexts;


/// <summary>
/// <inheritdoc cref="Context.IExecutionContext"/>
/// </summary>
internal sealed class ErgosfareExecutionContext( IDictionary<object, object?> items, CancellationToken cancellationToken)
    : IExecutionContext
{

    public CancellationToken CancellationToken { get; } = cancellationToken;
    public IDictionary<object, object?> Items { get; } = items;
    public object? MessageResult { get; set; }

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