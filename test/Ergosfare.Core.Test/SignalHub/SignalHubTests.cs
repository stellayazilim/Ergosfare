using System.Collections.Concurrent;
using System.Reflection;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.SignalHub;


/// <summary>
/// Unit tests for <see cref="EventHub"/> subscriptions, publishing, and pipeline events.
/// Covers weak and strong subscription behavior, publish/unsubscribe logic, 
/// and operator-based proxy events.
/// </summary>
public class SignalHubTests(SignalHubFixture signalHubFixture) : BaseSignalFixture(signalHubFixture)
{
    /// <summary>
    /// Verifies that subscribing to an event type registers the subscription internally.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldRegisterSubscriptions()
    {
        // Arrange & Act: subscribe to a BeginPipelineEvent
        SignalHubFixture.Hub.Subscribe<BeginPipelineSignal>(_ => { });
        
        // Access private _subscriptions dictionary via reflection
        var subsField = SignalHubFixture.Hub.GetType().GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(SignalHubFixture.Hub)!;

        // Assert: dictionary contains one subscription for BeginPipelineEvent
        Assert.True(dict.ContainsKey(typeof(BeginPipelineSignal)));
        Assert.Single(dict[typeof(BeginPipelineSignal)]);
        
        SignalHubFixture.Dispose();
    }

    /// <summary>
    /// Tests that publishing a <see cref="BeginPipelineSignal"/> triggers strong subscriptions.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldInvokeStrongSubscription()
    {
        SignalHubFixture = SignalHubFixture.New;
        var invoked = false;

        // Arrange: subscribe with strong reference
        SignalHubFixture.Hub.Subscribe<BeginPipelineSignal>(_ => invoked = true);

        // Act: publish the event
        SignalHubFixture.Hub.Publish(SignalHubFixture.BeginPipelineEvent<StubMessage>());

        // Assert: handler invoked
        Assert.True(invoked);
        SignalHubFixture.Dispose();
    }

    /// <summary>
    /// Ensures that dead weak subscriptions are removed after publishing.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Publish_ShouldRemoveDeadWeakSubscriptions()
    {
        SignalHubFixture = SignalHubFixture.New;
        var hub = SignalHubFixture.Hub;

        void CreateWeak()
        {
            // Arrange: weak subscription that will go out of scope
            var temp = new object();
            hub.Subscribe<BeginPipelineSignal>(_ => temp = temp.ToString(), useWeakReference: true);
        }

        CreateWeak();

        // Act: force garbage collection to simulate target collection
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Access internal subscriptions
        var subsField = SignalHubFixture.Hub.GetType().GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        var beforeCount = dict[typeof(BeginPipelineSignal)].Count;

        hub.Publish(Signal);

        var afterCount = dict[typeof(BeginPipelineSignal)].Count;

        // Assert: dead weak subscriptions removed, remaining alive
        Assert.True(afterCount <= beforeCount, "Dead weak subscriptions should be removed");
        Assert.All(dict[typeof(BeginPipelineSignal)],
            s => Assert.True(((ISubscription<BeginPipelineSignal>)s).IsAlive));

        SignalHubFixture.Dispose();
    }

    /// <summary>
    /// Ensures weak subscriptions return false and mark themselves dead when target is collected.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenCollected()
    {
        SignalHubFixture = SignalHubFixture.New;

        // Arrange: use stub weak reference to simulate collected target
        var stubWeak = new SignalHubFixture.StubWeakSubscription<Action<BeginPipelineSignal>>(null, isAlive: false);
        stubWeak.TryGetTarget(out var target);
        var weakSub = new WeakSubscription<BeginPipelineSignal>(target!);

        // Act: attempt to invoke the weak subscription
        var evt = new BeginPipelineSignal { Message = new StubMessage() };
        bool result = weakSub.Invoke(evt);

        // Assert: returns false and marked dead
        Assert.False(result);
        Assert.False(weakSub.IsAlive);
    }

    /// <summary>
    /// Verifies that invoking a strong subscription always returns true 
    /// and triggers the provided action.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void StrongSubscription_Invoke_ReturnsTrue()
    {
        SignalHubFixture = SignalHubFixture.New;
        var called = false;

        // Arrange: create a strong subscription with a simple action
        var strongSub = new StrongSubscription<BeginPipelineSignal>(_ => called = true);

        // Act: invoke the subscription with a dummy event
        var result = strongSub.Invoke(Signal);

        // Assert: action called, returns true, subscription alive
        Assert.True(result);
        Assert.True(called);
        Assert.True(strongSub.IsAlive);

        SignalHubFixture.Dispose();
    }

    /// <summary>
    /// Verifies that disposing a <see cref="StrongSubscription{TEvent}"/> does not throw.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void StrongSubscription_Dispose_DoesNotThrow()
    {
        // Arrange: create a strong subscription
        var strongSub = new StrongSubscription<BeginPipelineSignal>(_ => { });

        // Act & Assert: dispose safely without exceptions
        strongSub.Dispose();
    }

    /// <summary>
    /// Verifies that disposing a <see cref="WeakSubscription{TEvent}"/> does not throw.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void WeakSubscription_Dispose_DoesNotThrow()
    {
        // Arrange: create a weak subscription
        var weakSub = new WeakSubscription<BeginPipelineSignal>(_ => { });

        // Act & Assert: dispose safely without exceptions
        weakSub.Dispose();
    }
    
    
    
    /// <summary>
    /// Verifies that <see cref="StrongSubscription{TEvent}.Matches(Action{TEvent})"/> 
    /// correctly identifies the same delegate and rejects different ones.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void StrongSubscription_Matches_ReturnsTrueForSameAction()
    {
        // Arrange: define a delegate
        bool called = false;
        Action<BeginPipelineSignal> action = _ => called = true;
        var strongSub = new StrongSubscription<BeginPipelineSignal>(action);
        // act
        strongSub.Invoke(SignalHubFixture.BeginPipelineEvent<StubMessage>());
        // Assert: matching delegate returns true, different delegate returns false
        Assert.True(strongSub.Matches(action));
        Assert.False(strongSub.Matches(_ => { }));
        Assert.True(called);
    }
    
    
    /// <summary>
    /// Verifies that <see cref="WeakSubscription{TEvent}.Matches(Action{TEvent})"/> 
    /// correctly identifies the same delegate and rejects different ones.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void WeakSubscription_Matches_ReturnsTrueForSameAction()
    {
        // Arrange: define a delegate
        bool called = false;
        Action<BeginPipelineSignal> action = _ => called = true;
        var weakSub = new WeakSubscription<BeginPipelineSignal>(action);
        // invoke event to subscriber
        weakSub.Invoke(SignalHubFixture.BeginPipelineEvent<StubMessage>());
        // Act & Assert: matching delegate returns true, different delegate returns false
        Assert.True(weakSub.Matches(action));
        Assert.False(weakSub.Matches(_ => { }));

        // Ensure action was invoked at least once
        Assert.True(called);
    }
    
    
    
    /// <summary>
    /// Verifies that invoking a <see cref="WeakSubscription{TEvent}"/> returns true
    /// when the target is still alive.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void WeakSubscription_Invoke_ReturnsTrue_WhenTargetAlive()
    {
        // Arrange: keep a strong reference to prevent collection
        bool called = false;
        var strongRef = new object();
        Action<BeginPipelineSignal> action = e => { called = true; _ = strongRef; };
        var weakSub = new WeakSubscription<BeginPipelineSignal>(action);

        // Act: invoke the subscription
        bool result = weakSub.Invoke(Signal);

        // Assert: invocation succeeds and subscription is alive
        Assert.True(result);
        Assert.True(called);
        Assert.True(weakSub.IsAlive);
    }

    
    /// <summary>
    /// Verifies that invoking a <see cref="WeakSubscription{TEvent}"/> returns false
    /// when the target has been collected.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenTargetCollected()
    {
        // Arrange: simulate a collected target with stub weak reference
        var handler = (BeginPipelineSignal signal) => { };
        var stubWeak = new SignalHubFixture.StubWeakSubscription<Action<BeginPipelineSignal>>(null, isAlive: false);

        stubWeak.TryGetTarget(out var target);
        var weakSub = new WeakSubscription<BeginPipelineSignal>(target!);

        // Act: invoke the subscription
        var evt = new BeginPipelineSignal { Message = new StubMessage() };
        bool result = weakSub.Invoke(evt);

        // Assert: invocation fails and subscription is no longer alive
        Assert.False(result);
        Assert.False(weakSub.IsAlive);
    }
    
    
    
    /// <summary>
    /// Ensures that publishing an event when there are no subscribers
    /// does not throw an exception.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Publish_Returns_WhenNoSubscribers()
    {
        // Arrange: reset fixture
        SignalHubFixture = SignalHubFixture.New;

        // Act & Assert: publishing an event with no subscribers executes safely
        SignalHubFixture.Hub.Publish(SignalHubFixture.BeginPipelineEvent<StubMessage>());

        // Cleanup
        SignalHubFixture.Dispose();
    }


    /// <summary>
    /// Ensures that calling Unsubscribe on an event type with no subscriptions
    /// does not throw an exception.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_Returns_WhenNoSubscribersExist()
    {
        // Arrange: reset fixture
        SignalHubFixture = SignalHubFixture.New;

        // Act: attempt to unsubscribe a handler that was never added
        SignalHubFixture.Hub.Unsubscribe<BeginPipelineSignal>(_ => { });

        // Assert: safe execution (no exceptions thrown)
        SignalHubFixture.Dispose();
    }

    
    /// <summary>
    /// Verifies that unsubscribing a previously subscribed handler removes it
    /// and prevents future invocation.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_RemovesMatchingSubscription()
    {
        // Arrange: reset fixture and define a handler
        SignalHubFixture = SignalHubFixture.New;
        bool invoked = false;
        void Handler(BeginPipelineSignal e) => invoked = true;

        // Subscribe the handler
        SignalHubFixture.Hub.Subscribe<BeginPipelineSignal>(Handler);

        // Act: unsubscribe the same handler
        SignalHubFixture.Hub.Unsubscribe<BeginPipelineSignal>(Handler);

        // Publish event; handler should not be invoked
        SignalHubFixture.Hub.Publish(SignalHubFixture.BeginPipelineEvent<StubMessage>());

        // Assert: handler was removed successfully
        Assert.False(invoked, "Handler should have been removed by Unsubscribe");

        // Cleanup
        SignalHubFixture.Dispose();
    }
        
        
    /// <summary>
    /// Verifies that unsubscribing a previously subscribed handler removes it from
    /// the subscription list, including lambda/delegate scenarios.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_RemovesMatchingSubscription_LambdaCovered()
    {
        // Arrange: reset fixture and define a handler
        SignalHubFixture = SignalHubFixture.New;
        bool invoked = false;
        void Handler(BeginPipelineSignal e) => invoked = true;

        // Subscribe the handler
        SignalHubFixture.Hub.Subscribe<BeginPipelineSignal>(Handler);

        // Access private _subscriptions dictionary via reflection
        var subsField = SignalHubFixture.Hub.GetType().GetField("_subscriptions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(SignalHubFixture.Hub)!;

        // Ensure initially one subscription exists
        Assert.Single(dict[typeof(BeginPipelineSignal)]);

        // Act: unsubscribe the handler
        SignalHubFixture.Hub.Unsubscribe<BeginPipelineSignal>(Handler);

        // Assert: subscription list is now empty
        Assert.Empty(dict[typeof(BeginPipelineSignal)]);

        // Publish event to ensure the handler is not invoked
        SignalHubFixture.Hub.Publish(SignalHubFixture.BeginPipelineEvent<StubMessage>());
        Assert.False(invoked, "Handler should not be invoked after unsubscribe");

        // Cleanup
        SignalHubFixture.Dispose();
    }

    
    /// <summary>
    /// Ensures that non-subscription objects in the internal dictionary
    /// are ignored when calling Unsubscribe.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_IgnoresNonSubscriptionObjects()
    {
        // Arrange: reset fixture
        SignalHubFixture = SignalHubFixture.New;

        // Access _subscriptions dictionary via reflection
        var subsField = SignalHubFixture.Hub.GetType().GetField("_subscriptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(SignalHubFixture.Hub)!;

        // Add a dummy object to the subscription list
        var dummy = new object();
        dict.GetOrAdd(typeof(BeginPipelineSignal), _ => new List<object>()).Add(dummy);

        // Act: attempt to unsubscribe a handler that was never added
        SignalHubFixture.Hub.Unsubscribe<BeginPipelineSignal>(_ => { });

        // Assert: dummy object remains in the dictionary
        Assert.Contains(dummy, dict[typeof(BeginPipelineSignal)]);

        // Cleanup
        SignalHubFixture.Dispose();
    }

    
    /// <summary>
    /// Verifies that proxy events correctly support operator += and -=
    /// to subscribe and unsubscribe handlers.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ProxyEvent_OperatorPlusAndMinus_ShouldSubscribeAndUnsubscribe()
    {
        // Arrange: reset fixture
        SignalHubFixture = SignalHubFixture.New;
        var called = false;

        void Handler(BeginPipelineSignal e) => called = true;

        // Retrieve the proxy event
        var proxy = ((IHasProxySignals)SignalHubFixture.Hub).BeginPipelineSignal;

        // Act: subscribe using += operator
        proxy += Handler;

        // Publish event to trigger the handler
        SignalHubFixture.Hub.Publish(Signal);

        // Assert: handler invoked successfully
        Assert.True(called);

        // Reset flag
        called = false;

        // Act: unsubscribe using -= operator
        proxy -= Handler;

        // Publish event again
        SignalHubFixture.Hub.Publish(Signal);

        // Assert: handler not invoked after unsubscribe
        Assert.False(called);

        // Cleanup
        SignalHubFixture.Dispose();
    }
}
