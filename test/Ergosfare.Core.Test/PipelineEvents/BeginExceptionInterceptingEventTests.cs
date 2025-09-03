using Ergosfare.Core.Events;

namespace Ergosfare.Core.Test.PipelineEvents;

public class BeginExceptionInterceptingEventTests
{
    [Fact]
    public void Create_ShouldInitializePropertiesCorrectly()
    {
        // arrange
        var mediatorType = typeof(string);
        var messageType = typeof(int);
        var resultType = typeof(bool);
        var exception = new InvalidOperationException();
        ushort interceptorCount = 5;

        // act
        var ev = BeginExceptionInterceptingEvent.Create(
            mediatorType, messageType, resultType, exception, interceptorCount);

        // assert
        Assert.Equal(mediatorType, ev.MediatorInstance);
        Assert.Equal(messageType, ev.MessageType);
        Assert.Equal(resultType, ev.ResultType);
        Assert.Equal(exception, ev.Exception);
        Assert.Equal(interceptorCount, ev.InterceptorCount);
        Assert.True(ev.Timestamp <= DateTime.UtcNow); // optional timestamp check
    }

    [Fact]
    public void GetEqualityComponents_ShouldIncludeAllProperties()
    {
        // arrange
        var exception = new InvalidOperationException();
        var ev = BeginExceptionInterceptingEvent.Create(typeof(string), typeof(int), null, exception, 3);

        // act
        var components = ev.GetEqualityComponents().ToList();

        // assert
        Assert.Contains(ev.MediatorInstance, components);
        Assert.Contains(ev.MessageType, components);
        Assert.Contains(typeof(void), components); // ResultType null defaults to void
        Assert.Contains((ushort)3, components);
        Assert.Contains(exception, components);
    }
}