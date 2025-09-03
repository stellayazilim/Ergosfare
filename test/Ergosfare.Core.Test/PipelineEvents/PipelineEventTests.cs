using Ergosfare.Core.Events;

namespace Ergosfare.Core.Test.PipelineEvents;

public class PipelineEventTests
{
    private sealed class TestPipelineEvent : PipelineEvent;
    
    // Factory lambdas for all pipeline events
    public static readonly TheoryData<Func<PipelineEvent>> EventFactories = new()
    {
        () => BeginExceptionInterceptingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            exception: new InvalidOperationException(),
            interceptorCount: 5),

        () => FinishHandlerInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            handlerType: typeof(int),
            resultType: typeof(void)),

        () => BeginHandlerInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            handlerType: typeof(int),
            resultType: typeof(void)),
        
        () => BeginExceptionInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            handlerType: typeof(int),
            resultType: typeof(void),
            exception: new Exception()),
        
        ()=> BeginHandlingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            handlerCount: 5),
        
        ()=> BeginPipelineEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void)),
        
        ()=> BeginPostInterceptingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            interceptorCount: 5),
        
        () => BeginPostInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            interceptorType: typeof(int)
            ),
        
        ()=> BeginPreInterceptingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            interceptorCount: 5),
        
        ()=> BeginPreInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            interceptorType: typeof(int),
            resultType: typeof(void)),
        
        ()=> FinishPreInterceptingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void)),
        
        ()=> FinishExceptionInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            exception: new Exception()),
        
        ()=> FinishHandlingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void)),
        
        ()=> FinishHandlingWithExceptionEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            exception: new Exception()),
        
        ()=> FinishExceptionInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            exception: new Exception()),
        
        ()=> FinishExceptionInterceptingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            exception: new Exception(),
            finalException: new Exception()),
        
        () => FinishPipelineEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void)),
        
        ()=> FinishPostInterceptingEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            resultType: typeof(void),
            interceptorCount: 5),
        
        () => FinishPostInterceptingWithException.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            interceptorType: typeof(int),
            resultType: typeof(void),
            exception: new Exception()),
        
        () => FinishPostInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            interceptorType: typeof(string),
            resultType: typeof(void)),
        
        () => FinishPreInterceptingWithException.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            interceptorType: typeof(string),
            resultType: typeof(void),
            exception: new Exception()),
        
        () => FinishPreInterceptorInvocationEvent.Create(
            mediatorInstance: typeof(string),
            messageType: typeof(int),
            interceptorType: typeof(string),
            resultType: typeof(void))
    };
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void GetEqualityComponents_ShouldIncludeAllProperties()
    {
        // arrange
        var mediatorType = typeof(string);
        var messageType = typeof(int);
        var resultType = typeof(bool);

        var @event = new TestPipelineEvent
        {
            MediatorInstance = mediatorType,
            MessageType = messageType,
            ResultType = resultType
        };

        // act
        var components = @event.GetEqualityComponents().ToList();

        // assert
        Assert.Contains(mediatorType, components);
        Assert.Contains(messageType, components);
        Assert.Contains(resultType, components);
    }
    
    
    [Theory]
    [MemberData(nameof(EventFactories))]
    public void PipelineEvent_Factory_ShouldInitializeProperties(Func<PipelineEvent> factory)
    {
        // act
        var ev = factory();

        // assert
        Assert.NotNull(ev.MediatorInstance);
        Assert.NotNull(ev.MessageType);
        Assert.True(ev.Timestamp <= DateTime.UtcNow);

        // ResultType defaults to void if null
        if (ev.ResultType == null)
        {
            Assert.Equal(typeof(void), ev.GetEqualityComponents().Last());
        }
    }

    [Theory]
    [MemberData(nameof(EventFactories))]
    public void PipelineEvent_GetEqualityComponents_ShouldContainAllProperties(Func<PipelineEvent> factory)
    {
        // act
        var ev = factory();
        var components = ev.GetEqualityComponents().ToList();

        // assert common components
        Assert.Contains(ev.MediatorInstance, components);
        Assert.Contains(ev.MessageType, components);
        Assert.Contains(ev.ResultType ?? typeof(void), components);

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