using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Test.PipelineSignals;

public class BeginExceptionInterceptingSignalTests
{
    /// <summary>
    /// Unit tests for <see cref="BeginExceptionInterceptingSignal"/> class,
    /// verifying proper initialization and equality component behavior.
    /// </summary>
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
        var ev = BeginExceptionInterceptingSignal.Create("string", null, exception, interceptorCount);

        // assert
        Assert.Equal(exception, ev.Exception);
        Assert.Equal(interceptorCount, ev.InterceptorCount);
        Assert.True(ev.Timestamp <= DateTime.UtcNow); // optional timestamp check
    }

    /// <summary>
    /// Ensures that <see cref="BeginExceptionInterceptingSignal.GetEqualityComponents"/>
    /// includes all expected properties for equality comparison.
    /// </summary>
    [Fact]
    public void GetEqualityComponents_ShouldIncludeAllProperties()
    {
        // arrange
        var exception = new InvalidOperationException();
        var ev = BeginExceptionInterceptingSignal.Create("string",null, exception);

        // act
        var components = ev.GetEqualityComponents().ToList();

        // assert
        Assert.Contains("string", components); // ResultType null defaults to void
        Assert.Contains((ushort)0, components);
        Assert.Contains(exception, components);
    }
}