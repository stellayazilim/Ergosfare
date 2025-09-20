using Ergosfare.Commands.Abstractions;
using Ergosfare.Contracts.Attributes;
using Ergosfare.Core.Abstractions;
// ReSharper disable ClassNeverInstantiated.Global

namespace Ergosfare.Command.Test.__stubs__;



/// <summary>
/// A stub command pre-interceptor for <see cref="StubNonGenericCommand"/> used in tests.
/// Registered in "group1" with weight 2.
/// </summary>
[Group("group1")]
[Weight(2)]

public class StubCommandPreInterceptor1: ICommandPreInterceptor<StubNonGenericCommand>
{
    /// <summary>
    /// Indicates whether the interceptor has been called.
    /// </summary>
    public static  bool HasCalled;
    
    
    /// <summary>
    /// Handles the command asynchronously before its main handler.
    /// </summary>
    /// <param name="message">The command being intercepted.</param>
    /// <param name="context">The execution context for the pipeline.</param>
    /// <returns>The original command as an <see cref="object"/>.</returns>
    public virtual Task<object> HandleAsync(StubNonGenericCommand message, IExecutionContext context)
    {
        HasCalled = true;
        return Task.FromResult<object>(message);
    }
}

/// <summary>
/// A stub command pre-interceptor for <see cref="StubNonGenericCommand"/> used in tests.
/// Registered in "group1" and "group2" with weight 1.
/// </summary>
[Group("group1", "group2")]
[Weight(1)]
public class StubCommandPreInterceptor2: ICommandPreInterceptor<StubNonGenericCommand>
{
    /// <summary>
    /// Indicates whether the interceptor has been called.
    /// </summary>
    public static bool HasCalled;
    
    
    /// <summary>
    /// Handles the command asynchronously before its main handler.
    /// </summary>
    /// <param name="message">The command being intercepted.</param>
    /// <param name="context">The execution context for the pipeline.</param>
    /// <returns>The original command as an <see cref="object"/>.</returns>
    public virtual Task<object>  HandleAsync(StubNonGenericCommand message, IExecutionContext context)
    {
        HasCalled = true;
        return Task.FromResult<object>(message);
    }
}