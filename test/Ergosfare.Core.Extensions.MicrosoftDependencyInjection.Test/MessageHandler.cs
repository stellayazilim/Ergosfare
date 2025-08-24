using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

public sealed class MessageHandler: IAsyncHandler<Message>
{
 
    public Task HandleAsync(Message message, IExecutionContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Message received");
        return Task.CompletedTask;
    }
}