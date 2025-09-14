using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Extensions;

public static class MessageDependencyExtensions
{
    public static async Task RunAsyncPreInterceptors(this IMessageDependencies messageDependencies, object message, IExecutionContext context)
    {
        foreach (var preHandler in messageDependencies.IndirectPreInterceptors) 
            await (Task)  preHandler.Handler.Value.Handle(message, context);


        foreach (var preHandler in messageDependencies.PreInterceptors) 
            await (Task)preHandler.Handler.Value.Handle(message, context);
    }
    
    
    
    public static async Task RunAsyncPostInterceptors(this IMessageDependencies messageDependencies, object message, object? messageResult, IExecutionContext context)
    {
        
        foreach (var postInterCeptor in messageDependencies.PostInterceptors) 
            await (Task)postInterCeptor.Handler.Value.Handle(message, messageResult, context);

        foreach (var postInterCeptor in messageDependencies.IndirectPostInterceptors)
            await (Task)postInterCeptor.Handler.Value.Handle(message, messageResult, context);
    }
    
    
    
    public static async Task RunAsyncExceptionInterceptors(
        this IMessageDependencies messageDependencies, 
        object message, 
        object? messageResult, 
        ExceptionDispatchInfo exceptionDispatchInfo, 
        IExecutionContext context)
    {
        if (messageDependencies.ExceptionInterceptors.Count + messageDependencies.IndirectExceptionInterceptors.Count == 0)
            exceptionDispatchInfo.Throw();

        foreach (var errorHandler in messageDependencies.ExceptionInterceptors)
            await (Task) errorHandler.Handler.Value.Handle(message, messageResult, exceptionDispatchInfo.SourceException, context);
        


        foreach (var errorHandler in messageDependencies.IndirectExceptionInterceptors)
        {
            await (Task) errorHandler.Handler.Value.Handle(message, messageResult, exceptionDispatchInfo.SourceException,  context);
        }
    }


    public static async Task RunAsyncFinalInterceptors(
        this IMessageDependencies messageDependencies,
        object message,
        object? messageResult,
        Exception? exception,
        IExecutionContext context)
    {
        foreach (var finalHandler in messageDependencies.FinalInterceptors)
            await (Task) finalHandler.Handler.Value.Handle(message, messageResult, exception, context);
        


        foreach (var finalHandler in messageDependencies.FinalInterceptors)
        {
            await (Task) finalHandler.Handler.Value.Handle(message, messageResult, exception,  context);
        }
    }
}