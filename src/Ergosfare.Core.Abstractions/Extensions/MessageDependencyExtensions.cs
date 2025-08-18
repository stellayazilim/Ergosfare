using System.Threading.Tasks;
using Ergosfare.Core.Context;

namespace Ergosfare.Core.Abstractions.Extensions;

public static class MessageDependencyExtensions
{
    public static async Task RunAsyncPreInterceptors(this IMessageDependencies messageDependencies, object message, IExecutionContext context)
    {
        foreach (var preHandler in messageDependencies.IndirectPreInterceptors)
        {
            await (Task) preHandler.Handler.Value.Handle(message, context);
        }

        foreach (var preHandler in messageDependencies.PreInterceptors)
        {
            await (Task) preHandler.Handler.Value.Handle(message, context);
        }
    }
    
    
    
    public static async Task RunAsyncPostInterceptors(this IMessageDependencies messageDependencies, object message, object? messageResult, IExecutionContext context)
    {
        foreach (var postHandler in messageDependencies.PostInterceptors)
        {
            await (Task) postHandler.Handler.Value.Handle(message, messageResult, context);
        }

        foreach (var postHandler in messageDependencies.IndirectPostInterceptors)
        {
            await (Task) postHandler.Handler.Value.Handle(message, messageResult, context);
        }
    }
}