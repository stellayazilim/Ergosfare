﻿using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Ergosfare.Messaging.Abstractions.Context;
using Ergosfare.Messaging.Abstractions.Exceptions;

namespace Ergosfare.Messaging.Abstractions.Strategies;

/// <summary>
///     Represents a mediation strategy that processes a message through a single asynchronous handler.
/// </summary>
/// <typeparam name="TMessage">The type of message being mediated.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the handler.</typeparam>
/// <remarks>
///     This strategy ensures that only one handler is registered for the message type and then:
///     1. Executes pre-handlers.
///     2. Delegates the message processing to the registered handler.
///     3. Executes post-handlers.
///     In case of any exception during the process, it delegates the error handling to the registered error handlers.
/// </remarks>
public sealed class SingleAsyncHandlerMediationStrategy<TMessage, TResult> : IMessageMediationStrategy<TMessage, Task<TResult>> where TMessage : IMessage
{
    public async Task<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext executionContext)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }

        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }

        var handler = messageDependencies.Handlers.Single().Handler.Value;

        if (handler is null)
        {
            throw new InvalidOperationException($"Handler for {typeof(TMessage).Name} is not of the expected type.");
        }

        return await (Task<TResult>) handler.Handle(message);
    }
}