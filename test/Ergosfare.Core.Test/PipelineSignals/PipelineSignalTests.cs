using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.PipelineSignals;


/// <summary>
/// Unit tests for all pipeline signals, verifying that 
/// invoke methods, factory methods, and equality components behave as expected.
/// </summary>
public class PipelineSignalTests: IAsyncLifetime
{
    private static StubMessage Message { get; } = new StubMessage();
    private const string Result = "Hello World";
    private static Exception Exception { get; } = new Exception();
   
    /// <summary>
    /// Test data for invoking all pipeline signals.
    /// Each action corresponds to a signal's Invoke method.
    /// </summary>
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
        () => FinishHandlingWithExceptionSignal.Invoke(Message, Result,  Exception),
        () => FinishHandlingSignal.Invoke(Message, Result),
        () => BeginPostInterceptingSignal.Invoke(Message, Result, 3),
        () => BeginPostInterceptorInvocationSignal.Invoke(Message, Result, typeof(string)),
        () => FinishPostInterceptorInvocationSignal.Invoke(Message, Result),
        () => FinishPostInterceptingWithExceptionSignal.Invoke(Message, Result, typeof(string), Exception),
        () => FinishPostInterceptingSignal.Invoke(Message, Result),
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
    
    
    /// <summary>
    /// Factory lambdas to create instances of all pipeline signals for testing properties.
    /// </summary>
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
    
    /// <summary>
    /// Verifies that invoking all signals does not throw any exceptions.
    /// </summary>
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
    
    /// <summary>
    /// Verifies that signals created by factory methods initialize all properties correctly.
    /// </summary>
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
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

    /// <summary>
    /// Verifies that all equality components are included in <see cref="PipelineSignal.GetEqualityComponents"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(SignalFactories))]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
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
    
    /// <summary>
    /// Resets the <see cref="SignalHubAccessor"/> before each test.
    /// </summary>
    public Task InitializeAsync()
    {
        SignalHubAccessor.ResetInstance();
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Resets the <see cref="SignalHubAccessor"/> after each test.
    /// </summary>
    public Task DisposeAsync()
    {
        SignalHubAccessor.ResetInstance();
        return Task.CompletedTask;
    }
}