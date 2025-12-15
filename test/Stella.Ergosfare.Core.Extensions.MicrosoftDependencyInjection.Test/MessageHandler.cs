using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

/// <summary>
/// An asynchronous handler for <see cref="Message"/> objects.
/// </summary>
/// <remarks>
/// This handler simply writes a message to the console when a message is received.
/// </remarks>
public sealed class MessageHandler: IAsyncHandler<Message>
{
 
    /// <summary>
    /// Handles the specified <paramref name="message"/> asynchronously.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context providing services and metadata.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task HandleAsync(Message message, IExecutionContext context)
    {
        Console.WriteLine("Message received");
        return Task.CompletedTask;
    }
}