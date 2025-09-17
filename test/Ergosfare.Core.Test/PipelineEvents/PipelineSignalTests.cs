using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.PipelineEvents;

public class PipelineSignalTests: IAsyncLifetime
{
    private static StubMessage Message { get; } = new StubMessage();
    private const string Result = "Hello World";
    private static Exception Exception { get; } = new Exception();
   
    public static TheoryData<Action> SignalInvokeData =
    [
        () => BeginPreInterceptingSignal.Invoke(Message, Result, 2),
        () => FinishPreInterceptingSignal.Invoke(Message, Result),
        () => BeginPreInterceptorInvocationSignal.Invoke(Message, Result, typeof(string)),
        () => FinishPreInterceptorInvocationSignal.Invoke(Message, Result),
        () => FinishPreInterceptingWithExceptionSignal.Invoke(Message, Result, typeof(string), Exception),

        () => BeginHandlingSignal.Invoke(Message, Result),
        () => BeginHandlerInvocationSignal.Invoke(Message, Result, typeof(string)),
        () => FinishHandlerInvocationSignal.Invoke(Message, Result, typeof(string)),

        () => BeginPostInterceptingSignal.Invoke(Message, Result, 3),
        () => FinishPostInterceptingSignal.Invoke(Message, Result),
        () => FinishHandlingSignal.Invoke(Message, Result),
        () => FinishPostInterceptorInvocationSignal.Invoke(Message, Result),
        () => FinishPostInterceptingWithExceptionSignal.Invoke(Message, Result, typeof(string), Exception),

        () => BeginExceptionInterceptingSignal.Invoke(Message, Result, Exception, 4),
        () => FinishExceptionInterceptingSignal.Invoke(Message, Result, Exception),
        () => BeginExceptionInterceptorInvocationSignal.Invoke(Message, Result, typeof(string), Exception),
        () => FinishExceptionInterceptorInvocationSignal.Invoke(Message, Result, Exception),

        () => BeginFinalInterceptingSignal.Invoke(Message, Result, Exception, 5),
        () => FinishFinalInterceptingSignal.Invoke(Message, Result),
        () => BeginFinalInterceptorInvocationSignal.Invoke(Message, Result, Exception, typeof(string)),
        () => FinishFinalInterceptorInvocationSignal.Invoke(Message, Result),

        () => BeginPipelineSignal.Invoke(Message, Result),
        () => FinishPipelineSignal.Invoke(Message, Result),
    ];
    
    
    // Factory lambdas for all pipeline events
    public static readonly TheoryData<Func<PipelineSignal>> SignalFactories =
    [
        () => BeginExceptionInterceptingSignal.Create(
            message: "string",
            result: null,
            exception: new InvalidOperationException(),
            interceptorCount: 5),


        () => FinishHandlerInvocationSignal.Create(
            message: "string",
            result: null,
            handlerType: typeof(int)),

        () => BeginHandlerInvocationSignal.Create(
            message: "string",
            result: null,
            handlerType: typeof(int)),


        () => BeginExceptionInterceptorInvocationSignal.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int),
            exception: new Exception()),


        () => BeginHandlingSignal.Create(
            message: "string",
            result: null,
            handlerCount: 5),


        () => BeginPipelineSignal.Create(
            message: "string",
            result: null),


        () => BeginPostInterceptingSignal.Create(
            message: "string",
            result: null,
            interceptorCount: 5),


        () => BeginPostInterceptorInvocationSignal.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int)
        ),


        () => BeginPreInterceptingSignal.Create(
            message: "string",
            result: null,
            interceptorCount: 5),


        () => BeginPreInterceptorInvocationSignal.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int)),


        () => FinishPreInterceptingSignal.Create(
            message: "string",
            result: null),


        () => FinishExceptionInterceptorInvocationSignal.Create(
            message: "string",
            result: null,
            exception: new Exception()),


        () => FinishHandlingSignal.Create(
            message: "string",
            result: null),


        () => FinishHandlingWithExceptionSignal.Create(
            message: "string",
            result: null,
            exception: new Exception()),


        () => FinishExceptionInterceptorInvocationSignal.Create(
            message: "string",
            result: null,
            exception: new Exception()),


        () => FinishExceptionInterceptingSignal.Create(
            message: "string",
            result: null,
            exception: new Exception()),


        () => FinishPipelineSignal.Create(
            message: "string",
            result: null),


        () => FinishPostInterceptingSignal.Create(
            message: "string",
            result: null),


        () => FinishPostInterceptingWithExceptionSignal.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int),
            exception: new Exception()),


        () => FinishPostInterceptorInvocationSignal.Create(
            message: "string",
            result: null),


        () => FinishPreInterceptingWithExceptionSignal.Create(
            message: "string",
            result: null,
            interceptorType: typeof(string),
            exception: new Exception()),


        () => FinishPreInterceptorInvocationSignal.Create(
            message: "string",
            result: null),

        () => BeginFinalInterceptingSignal.Create(
            message: "string",
            result: null,
            interceptorCount: 5,
            exception: null
        ),

        () => BeginFinalInterceptorInvocationSignal.Create(
            message: "string",
            result: null,
            exception: null,
            interceptorType: typeof(int)
        ),

        () => FinishFinalInterceptorInvocationSignal.Create(
            message: "string",
            result: null),

        () => FinishFinalInterceptingSignal.Create(
            message: "string",
            result: null)
    ];
    
    
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    [Theory]
    [MemberData(nameof(SignalInvokeData))]
    public void Invoke_ShouldNotThrow(Action invokeAction)
    {
        // Act & Assert
        var ex = Record.Exception(invokeAction);
        Assert.Null(ex); // all invokes must succeed
    }
    
    [Theory]
    [MemberData(nameof(SignalFactories))]
    public void PipelineSignal_Factory_ShouldInitializeProperties(Func<PipelineSignal> factory)
    {
        // act
        var ev = factory();

        // assert

        Assert.True(ev.Timestamp <= DateTime.UtcNow);
        Assert.Equal("string",ev.Message);
    }

    [Theory]
    [MemberData(nameof(SignalFactories))]
    public void PipelineSignal_GetEqualityComponents_ShouldContainAllProperties(Func<PipelineSignal> factory)
    {
        // act
        var ev = factory();
        var components = ev.GetEqualityComponents().ToList();


        // For events with extra properties like InterceptorCount or Exception, check dynamically
        var type = ev.GetType();

        if (type.GetProperty("InterceptorCount") != null)
        {
            var count = (ushort)type.GetProperty("InterceptorCount")!.GetValue(ev)!;
            Assert.Contains(count, components);
        }

        if (type.GetProperty("Exception") != null)
        {
            var ex = (Exception)type.GetProperty("Exception")!.GetValue(ev)!;
            Assert.Contains(ex, components);
        }
    }

    public Task InitializeAsync()
    {
        SignalHubAccessor.ResetInstance();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SignalHubAccessor.ResetInstance();
        return Task.CompletedTask;
    }
}