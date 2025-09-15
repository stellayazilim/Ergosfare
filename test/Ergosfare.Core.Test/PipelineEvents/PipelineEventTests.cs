using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Core.Test.PipelineEvents;

public class PipelineEventTests
{
    
       // Test data: event type, invoke action
    public static IEnumerable<object[]> EventInvokeData()
    {
        object dummyMessage = new object();
        object dummyResult = new object();
        Exception dummyException = new InvalidOperationException("test");

        yield return new object[]
        {
            "BeginPreInterceptingEvent",
            new Action(() => BeginPreInterceptingEvent.Invoke(dummyMessage, dummyResult, 2))
        };
        yield return new object[]
        {
            "FinishPreInterceptingEvent",
            new Action(() => FinishPreInterceptingEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "BeginPreInterceptorInvocationEvent",
            new Action(() => BeginPreInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult, typeof(string)))
        };
        yield return new object[]
        {
            "FinishPreInterceptorInvocationEvent",
            new Action(() => FinishPreInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult))
        };

        yield return new object[]
        {
            "FinishPreInterceptingWithExceptionEvent",
            new Action(() => FinishPreInterceptingWithExceptionEvent.Invoke(dummyMessage, dummyResult, typeof(string), dummyException))
        };
        
        yield return new object[]
        {
            "BeginHandlingEvent",
            new Action(() => BeginHandlingEvent.Invoke(dummyMessage, dummyResult, 0))
        };
        
        yield return new object[]
        {
            "BeginHandlerInvocationEvent",
            new Action(() => BeginHandlerInvocationEvent.Invoke(dummyMessage, dummyResult, typeof(string)))
        };
        
        yield return new object[]
        {
            "FinishHandlerInvocationEvent",
            new Action(() => FinishHandlerInvocationEvent.Invoke(dummyMessage, dummyResult, typeof(string)))
        };
        
        yield return new object[]
        {
            "FinishHandlerInvocationEvent",
            new Action(() => FinishHandlerInvocationEvent.Invoke(dummyMessage, dummyResult, typeof(string)))
        };
        
        yield return new object[]
        {
            "BeginPostInterceptingEvent",
            new Action(() => BeginPostInterceptingEvent.Invoke(dummyMessage, dummyResult, 3))
        };
        yield return new object[]
        {
            "FinishPostInterceptingEvent",
            new Action(() => FinishPostInterceptingEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "FinishHandlingEvent",
            new Action(() => FinishHandlingEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "FinishPostInterceptorInvocationEvent",
            new Action(() => FinishPostInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "FinishPostInterceptingWithExceptionEvent",
            new Action(() => FinishPostInterceptingWithExceptionEvent.Invoke(dummyMessage, dummyResult, typeof(string), dummyException))
        };
        yield return new object[]
        {
            "BeginExceptionInterceptingEvent",
            new Action(() => BeginExceptionInterceptingEvent.Invoke(dummyMessage, dummyResult, dummyException, 4))
        };
        yield return new object[]
        {
            "FinishExceptionInterceptingEvent",
            new Action(() => FinishExceptionInterceptingEvent.Invoke(dummyMessage, dummyResult, dummyException))
        };
        yield return new object[]
        {
            "BeginExceptionInterceptorInvocationEvent",
            new Action(() => BeginExceptionInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult, typeof(string), dummyException))
        };
        yield return new object[]
        {
            "FinishExceptionInterceptorInvocationEvent",
            new Action(() => FinishExceptionInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult, dummyException))
        };
        yield return new object[]
        {
            "BeginFinalInterceptingEvent",
            new Action(() => BeginFinalInterceptingEvent.Invoke(dummyMessage, dummyResult, dummyException, 5))
        };
        yield return new object[]
        {
            "FinishFinalInterceptingEvent",
            new Action(() => FinishFinalInterceptingEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "BeginFinalInterceptorInvocationEvent",
            new Action(() => BeginFinalInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult, dummyException, typeof(string)))
        };
        yield return new object[]
        {
            "FinishFinalInterceptorInvocationEvent",
            new Action(() => FinishFinalInterceptorInvocationEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "BeginPipelineEvent",
            new Action(() => BeginPipelineEvent.Invoke(dummyMessage, dummyResult))
        };
        yield return new object[]
        {
            "FinishPipelineEvent",
            new Action(() => FinishPipelineEvent.Invoke(dummyMessage, dummyResult))
        };
    }

    
    
    // Factory lambdas for all pipeline events
    public static readonly TheoryData<Func<PipelineEvent>> EventFactories = new()
    {
        () => BeginExceptionInterceptingEvent.Create(
            message: "string",
            result: null,
            exception: new InvalidOperationException(),
            interceptorCount: 5),

        () => FinishHandlerInvocationEvent.Create(
            message: "string",
            result: null,
            handlerType: typeof(int)),
        () => BeginHandlerInvocationEvent.Create(
            message: "string",
            result: null,
            handlerType: typeof(int)),
        
        () => BeginExceptionInterceptorInvocationEvent.Create(
            message: "string",
            result: null,
            handlerType: typeof(int),
            exception: new Exception()),
        
        ()=> BeginHandlingEvent.Create(
            message: "string",
            result: null,
            handlerCount: 5),
        
        ()=> BeginPipelineEvent.Create(
            message: "string",
            result: null),
        
        ()=> BeginPostInterceptingEvent.Create(
            message: "string",
            result: null,
            interceptorCount: 5),
        
        () => BeginPostInterceptorInvocationEvent.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int)
            ),
        
        ()=> BeginPreInterceptingEvent.Create(
            message: "string",
            result: null,
            interceptorCount: 5),
        
        ()=> BeginPreInterceptorInvocationEvent.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int)),
        
        ()=> FinishPreInterceptingEvent.Create(
            message: "string",
            result: null),
        
        ()=> FinishExceptionInterceptorInvocationEvent.Create(
            message: "string",
            result: null,
            exception: new Exception()),
        
        ()=> FinishHandlingEvent.Create(
            message: "string",
            result: null),
        
        ()=> FinishHandlingWithExceptionEvent.Create(
            message: "string",
            result: null,
            exception: new Exception()),
        
        ()=> FinishExceptionInterceptorInvocationEvent.Create(
            message: "string",
            result: null,
            exception: new Exception()),
        
        ()=> FinishExceptionInterceptingEvent.Create(
            message: "string",
            result: null,
            exception: new Exception()),
        
        () => FinishPipelineEvent.Create(
            message: "string",
            result: null),
        
        ()=> FinishPostInterceptingEvent.Create(
            message: "string",
            result: null),
        
        () => FinishPostInterceptingWithExceptionEvent.Create(
            message: "string",
            result: null,
            interceptorType: typeof(int),
            exception: new Exception()),
        
        () => FinishPostInterceptorInvocationEvent.Create(
            message: "string",
            result: null),
        
        () => FinishPreInterceptingWithExceptionEvent.Create(
            message: "string",
            result: null,
            interceptorType: typeof(string),
            exception: new Exception()),
        
        () => FinishPreInterceptorInvocationEvent.Create(
            message: "string",
            result: null),
        () => BeginFinalInterceptingEvent.Create(
            message:  "string",
            result: null,
            interceptorCount: 5,
            exception: null
            ),
        () => BeginFinalInterceptorInvocationEvent.Create(
            message: "string",
            result: null,
            exception: null,
            interceptorType: typeof(int)
            ),
        () => FinishFinalInterceptorInvocationEvent.Create(
            message: "string",
            result: null),
        () => FinishFinalInterceptingEvent.Create(
            message:  "string",
            result: null)
    };
    
    
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    [Theory]
    [MemberData(nameof(EventInvokeData))]
    public void Invoke_ShouldNotThrow(string eventName, Action invokeAction)
    {
        // Act & Assert
        var ex = Record.Exception(invokeAction);
        Assert.Null(ex); // all invokes must succeed
    }
    
    [Theory]
    [MemberData(nameof(EventFactories))]
    public void PipelineEvent_Factory_ShouldInitializeProperties(Func<PipelineEvent> factory)
    {
        // act
        var ev = factory();

        // assert

        Assert.True(ev.Timestamp <= DateTime.UtcNow);
        Assert.Equal("string",ev.Message);
    }

    [Theory]
    [MemberData(nameof(EventFactories))]
    public void PipelineEvent_GetEqualityComponents_ShouldContainAllProperties(Func<PipelineEvent> factory)
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
}