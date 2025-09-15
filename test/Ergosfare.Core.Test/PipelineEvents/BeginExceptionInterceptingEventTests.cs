using Ergosfare.Core.Abstractions.Events;

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
        var ev = BeginExceptionInterceptingEvent.Create("string", null, exception, interceptorCount);

        // assert
        Assert.Equal(exception, ev.Exception);
        Assert.Equal(interceptorCount, ev.InterceptorCount);
        Assert.True(ev.Timestamp <= DateTime.UtcNow); // optional timestamp check
    }

    [Fact]
    public void GetEqualityComponents_ShouldIncludeAllProperties()
    {
        // arrange
        var exception = new InvalidOperationException();
        var ev = BeginExceptionInterceptingEvent.Create("string",null, exception);

        // act
        var components = ev.GetEqualityComponents().ToList();

        // assert
        Assert.Contains("string", components); // ResultType null defaults to void
        Assert.Contains((ushort)0, components);
        Assert.Contains(exception, components);
    }
}