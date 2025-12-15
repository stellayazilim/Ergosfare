using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.SignalHub;
using Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Test.Fixtures;

/// <summary>
/// Provides a dedicated fixture for testing the <see cref="ISignalHub"/> pipeline and related events.
/// Implements <see cref="IFixture{TFixture}"/> and <see cref="IAsyncDisposable"/> to support test isolation and cleanup.
/// </summary>
/// <remarks>
/// This fixture allows creating pre-configured event instances for main handlers, pre/post/interceptors,
/// exception handling, and final interceptors. Each method produces a strongly-typed event for testing purposes.
/// The fixture ensures a clean <see cref="SignalHubAccessor"/> state before and after tests.
/// </remarks>
public class SignalFixture : IFixture<SignalFixture>, IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the singleton <see cref="ISignalHub"/> instance managed by <see cref="SignalHubAccessor"/>.
    /// Provides access to the in-memory event hub for publishing and subscribing to pipeline and handler events.
    /// </summary>
    public ISignalHub Hub => SignalHubAccessor.Instance;
    
    /// <summary>
    /// Initializes a new instance of <see cref="SignalFixture"/> and resets the <see cref="SignalHubAccessor"/>.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public SignalFixture()
    {
        SignalHubAccessor.ResetInstance();
    }

    /// <summary>
    /// Returns a new instance of <see cref="SignalFixture"/> for fluent usage.
    /// </summary>
    public SignalFixture New => new();

    /// <summary>
    /// No-op method for compatibility with <see cref="IFixture{T}"/>. 
    /// This fixture does not interact with dependency injection.
    /// </summary>
    /// <param name="configure">Unused service configuration delegate.</param>
    /// <returns>The current fixture instance.</returns>
    public SignalFixture AddServices(Action<IServiceCollection> configure) => this;

    /// <summary>
    /// Always null, because this fixture does not provide DI services.
    /// </summary>
    public ServiceProvider ServiceProvider { get; } = null!;

    #region Pipeline events
    /// <summary>
    /// Returns a pre-configured <see cref="BeginPipelineSignal"/> with a stub message.
    /// </summary>
    public BeginPipelineSignal BeginPipelineEvent<TMessage>() 
        where TMessage : notnull, new() => new()
    {
        Message = new TMessage(),
        Result = null
    };

    /// <summary>
    /// Returns a <see cref="FinishPipelineEvent{TMessage, TResult}"/> for the specified message/result types.
    /// </summary>
    public FinishPipelineSignal FinishPipelineEvent<TMessage, TResult>()
        where TMessage : notnull, new()
        where TResult : new() => new()
    {
        Message = new TMessage(),
        Result = new TResult()
    };
    #endregion

    #region Main-Handler events
    /// <summary>
    /// Returns a <see cref="BeginHandlingEvent{TMessage}"/> for testing handler execution start.
    /// </summary>
    public BeginHandlingSignal BeginHandlingEvent<TMessage>(ushort handlerCount = 0)
        where TMessage : notnull, new() => new()
    {
        Message = new TMessage(),
        Result = null,
        HandlerCount = handlerCount
    };

    /// <summary>
    /// Returns a <see cref="BeginHandlerInvocationEvent{TMessage, THandler}"/> for testing handler invocation start.
    /// </summary>
    public BeginHandlerInvocationSignal BeginHandlerInvocationEvent<TMessage, THandler>()
        where TMessage : notnull, new()
        where THandler : IHandler => new()
    {
        Message = new TMessage(),
        Result = null,
        HandlerType = typeof(THandler)
    };

    /// <summary>
    /// Returns a <see cref="FinishHandlerInvocationEvent{TMessage, TResult, THandler}"/> for handler completion.
    /// </summary>
    public FinishHandlerInvocationSignal FinishHandlerInvocationEvent<TMessage, TResult, THandler>()
        where TMessage : notnull, new()
        where TResult : new()
        where THandler : IHandler => new()
    {
        Message = new TMessage(),
        Result = new TResult(),
        HandlerType = typeof(THandler)
    };

    /// <summary>
    /// Returns a <see cref="FinishHandlingWithExceptionEvent{TMessage, TResult}"/> to simulate a handler exception.
    /// </summary>
    public FinishHandlingWithExceptionSignal FinishHandlingWithExceptionEvent<TMessage, TResult>(Exception exception)
        where TMessage : notnull, new()
        where TResult : new() => new()
    {
        Message = new TMessage(),
        Result = new TResult(),
        Exception = exception,
    };

    /// <summary>
    /// Returns a <see cref="FinishHandlingEvent{TMessage, TResult}"/> for handler completion without exceptions.
    /// </summary>
    public FinishHandlingSignal FinishHandlingEvent<TMessage, TResult>()
        where TMessage : notnull, new()
        where TResult : new() => new()
    {
        Message = new TMessage(),
        Result = new TResult(),
    };
    #endregion

    #region Pre-Interceptor events
    /// <summary>
    /// Returns a <see cref="BeginPreInterceptingEvent{TMessage}"/> for testing pre-interceptor pipeline start.
    /// </summary>
    public BeginPreInterceptingSignal BeginPreInterceptingEvent<TMessage>(ushort interceptorCount = 1)
        where TMessage : notnull, new() => new()
    {
        Message = new TMessage(),
        InterceptorCount = interceptorCount
    };

    /// <summary>
    /// Returns a <see cref="BeginPreInterceptorInvocationEvent{TMessage, TInterceptor}"/> for testing individual pre-interceptor execution.
    /// </summary>
    public BeginPreInterceptorInvocationSignal BeginPreInterceptorInvocationEvent<TMessage, TInterceptor>()
        where TMessage : notnull, new()
        where TInterceptor : IPreInterceptor => new()
    {
        Message = new TMessage(),
        InterceptorType = typeof(TInterceptor)
    };

    /// <summary>
    /// Returns a <see cref="FinishPreInterceptorInvocationEvent{TMessage}"/> for testing pre-interceptor completion.
    /// </summary>
    public FinishPreInterceptorInvocationSignal FinishPreInterceptorInvocationEvent<TMessage>()
        where TMessage : notnull, new() => new()
    {
        Message = new TMessage(),
    };

    /// <summary>
    /// Returns a <see cref="FinishPreInterceptingWithExceptionEvent{TMessage, TInterceptor}"/> to simulate pre-interceptor exceptions.
    /// </summary>
    public FinishPreInterceptingWithExceptionSignal FinishPreInterceptingWithExceptionEvent<TMessage, TInterceptor>(Exception exception)
        where TMessage : notnull, new()
        where TInterceptor : IPreInterceptor => new()
    {
        Message = new TMessage(),
        Exception = exception,
        InterceptorType = typeof(TInterceptor)
    };

    /// <summary>
    /// Returns a <see cref="FinishPreInterceptingEvent{TMessage}"/> for testing pre-interceptor pipeline completion.
    /// </summary>
    public FinishPreInterceptingSignal FinishPreInterceptingEvent<TMessage>()
        where TMessage : notnull, new() => new()
    {
        Message = new TMessage(),
    };
    #endregion

    /// <summary>
    /// A test stub for <see cref="WeakReference{T}"/> that allows controlling
    /// whether the target is considered alive or collected.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    public class StubWeakSubscription<T> where T : class
    {
        private readonly T? _target;
        private readonly bool _isAlive;

        /// <summary>
        /// Creates a new stub weak reference.
        /// </summary>
        /// <param name="target">The object to store.</param>
        /// <param name="isAlive">If true, TryGetTarget returns the object; otherwise null.</param>
        public StubWeakSubscription(T? target, bool isAlive = true)
        {
            _target = target;
            _isAlive = isAlive;
        }

        /// <summary>
        /// Tries to get the target object.
        /// </summary>
        public bool TryGetTarget(out T? target)
        {
            target = _isAlive ? _target : null;
            return _isAlive && _target is not null;
        }
    }
    
    
    /// <summary>
    /// A stub implementation of <see cref="ISubscription{T}"/> that holds a strong reference to the action.
    /// Always alive and immediately invokes the action when called.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Signal"/> handled.</typeparam>
    public sealed class StubStrongSubscription<T> : ISubscription<T> where T : Signal
    {
        /// <summary>
        /// The action invoked when the subscription is triggered.
        /// </summary>
        private readonly Action<T> _action;

        /// <summary>
        /// Initializes a new strong subscription with the given action.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        public StubStrongSubscription(Action<T> action) => _action = action;

        /// <summary>
        /// Invokes the subscription's action.
        /// </summary>
        /// <param name="event">The event to pass to the action.</param>
        /// <returns>Always returns <c>true</c>.</returns>
        public bool Invoke(T @event)
        {
            _action(@event);
            return true;
        }

        /// <summary>
        /// Determines whether the given action matches this subscription.
        /// </summary>
        /// <param name="action">The action to compare.</param>
        /// <returns><c>true</c> if the actions are equal; otherwise <c>false</c>.</returns>
        public bool Matches(Action<T> action) => _action.Equals(action);

        /// <summary>
        /// Indicates whether the subscription is alive.
        /// Always <c>true</c> for strong subscriptions.
        /// </summary>
        private bool _isAlive = true;
        public bool IsAlive => _isAlive;

        /// <summary>
        /// Disposes the subscription.
        /// </summary>
        public void Dispose() => _isAlive = false;
       
    }
    
    /// <summary>
    /// Disposes the <see cref="SignalFixture"/> instance,
    /// ensuring that the <see cref="SignalHubAccessor"/> is reset to a clean state.
    /// This helps maintain test isolation between different test classes or runs.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        SignalHubAccessor.ResetInstance();
        _disposed = true;
    }
    
    /// <summary>
    /// A stub implementation of <see cref="Signal"/> used for testing purposes.
    /// Provides two simple equality components (<see cref="Value1"/> and <see cref="Value2"/>).
    /// </summary>
    public class StubSignal: Signal
    {
        
        /// <summary>
        /// Gets or sets the first test value used as part of the equality comparison.
        /// </summary>
        public required int Value1 { get; init; }
        
        /// <summary>
        /// Gets or sets the second test value used as part of the equality comparison.
        /// </summary>
        public required string Value2 { get; init; }
        
        
        /// <summary>
        /// Provides the equality components for this stub signal.
        /// Includes <see cref="Value1"/> and <see cref="Value2"/>.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{Object}"/> containing the values that define equality.
        /// </returns>
        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return Value1;
            yield return Value2;
        }
    }
    
    
    
    /// <summary>
    /// Creates a new <see cref="StubSignal"/> with the given test values.
    /// Useful for simplifying signal creation in tests.
    /// </summary>
    /// <param name="v1">The integer value used as the first equality component.</param>
    /// <param name="v2">The string value used as the second equality component.</param>
    /// <returns>A new instance of <see cref="StubSignal"/> initialized with the specified values.</returns>
    public StubSignal CreateSignal(int v1, string v2) => new ()  { Value1 = v1, Value2 = v2 };


    /// <summary>
    /// Asynchronously disposes the fixture. Internally calls <see cref="Dispose"/>.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
