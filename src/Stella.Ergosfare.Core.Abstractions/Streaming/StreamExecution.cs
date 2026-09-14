#pragma warning disable ERGOEXP003
using System.ComponentModel;

namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>Lifecycle bridge used by source-generated plans across assembly boundaries.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class StreamExecution
{
    /// <summary>Waits for a bound source to finish its cancellation and cleanup.</summary>
    public static ValueTask WaitForProducerAsync(object message)
        => message is ErgosfareStream stream ? stream.WaitForProducerAsync() : default;

    /// <summary>Closes a stream message's input when its generated execution ends.</summary>
    /// <param name="message">The dispatched message.</param>
    /// <param name="exception">The terminal failure, if any.</param>
    public static void EndInput(object message, Exception? exception)
    {
        if (message is ErgosfareStream stream)
            stream.EndDispatch(exception);
    }
}
