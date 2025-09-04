using System.Collections.Concurrent;
using System.Reflection;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.EventHub;
using Ergosfare.Core.Events;
using Hub = Ergosfare.Core.EventHub.EventHub;

namespace Ergosfare.Core.Test.EventHub;

public class EventHubTests
{
    private BeginPipelineEvent _event = new ()
    {
        MediatorInstance = typeof(string),
        MessageType = typeof(string),
        ResultType = typeof(string)
    };
    // helper class
    private class Target
    {
        public void Handler(BeginPipelineEvent e) { /* do nothing */ }
    }
    
    
    private sealed class UnsubscribedEvent : HubEvent
    {
        public override IEnumerable<object> GetEqualityComponents()
        {
            yield break;
        }
    }

    
    private sealed class TestEvent : HubEvent
    {
        public override IEnumerable<object> GetEqualityComponents() => Enumerable.Empty<object>();
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldRegisterSubscriptions()
    {
        var hub = new Hub();

        hub.Subscribe<BeginPipelineEvent>(_ => { });

        var subsField = typeof(Hub).GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        Assert.True(dict.ContainsKey(typeof(BeginPipelineEvent)));
        Assert.Single(dict[typeof(BeginPipelineEvent)]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldInvokeStrongSubscription()
    {
        var hub  = new Hub();
        var invoked = false;

        hub.Subscribe<BeginPipelineEvent>(_ => invoked = true);

        hub.Publish(_event);

        Assert.True(invoked);
    }

    [Fact]
    public void Publish_ShouldRemoveDeadWeakSubscriptions()
    {
        var hub  = new Hub();

        void CreateWeak()
        {
            var temp = new object();
            hub.Subscribe<BeginPipelineEvent>(_ => temp = temp.ToString(), useWeakReference: true);
        }

        CreateWeak();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var subsField = typeof(Hub).GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        var beforeCount = dict[typeof(BeginPipelineEvent)].Count;

        hub.Publish(_event);

        var afterCount = dict[typeof(BeginPipelineEvent)].Count;

        Assert.True(afterCount <= beforeCount, "Dead weak subscriptions should be removed");
        Assert.All(dict[typeof(BeginPipelineEvent)],
            s => Assert.True(((ISubscription<BeginPipelineEvent>)s).IsAlive));
    }

    [Fact]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenCollected()
    {
        WeakSubscription<BeginPipelineEvent> weakSub = null!;

        void CreateWeak()
        {
            var temp = new object();
            weakSub = new WeakSubscription<BeginPipelineEvent>(_ => temp = temp.ToString());
        }

        CreateWeak();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var result = weakSub.Invoke(_event);

        Assert.False(result);
        Assert.False(weakSub.IsAlive);
    }

    [Fact]
    public void StrongSubscription_Invoke_ReturnsTrue()
    {
        var called = false;
        var strongSub = new StrongSubscription<BeginPipelineEvent>(_ => called = true);

        var result = strongSub.Invoke(_event);

        Assert.True(result);
        Assert.True(called);
        Assert.True(strongSub.IsAlive);
    }

    [Fact]
    public void StrongSubscription_Dispose_DoesNotThrow()
    {
        var strongSub = new StrongSubscription<BeginPipelineEvent>(_ => { });
        strongSub.Dispose();
    }

    [Fact]
    public void WeakSubscription_Dispose_DoesNotThrow()
    {
        var weakSub = new WeakSubscription<BeginPipelineEvent>(_ => { });
        weakSub.Dispose();
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void StrongSubscription_Matches_ReturnsTrueForSameAction()
    {
        // Arrange
        bool called = false;
        Action<BeginPipelineEvent> action = _ => called = true;
        var strongSub = new StrongSubscription<BeginPipelineEvent>(action);

        // Act & Assert
        Assert.True(strongSub.Matches(action)); // should match the same delegate
        Assert.False(strongSub.Matches(_ => { })); // different delegate should not match
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void WeakSubscription_Matches_ReturnsTrueForSameAction()
    {
        // Arrange
        bool called = false;
        Action<BeginPipelineEvent> action = _ => called = true;
        var weakSub = new WeakSubscription<BeginPipelineEvent>(action);

        // Act & Assert
        Assert.True(weakSub.Matches(action)); // should match the same delegate
        Assert.False(weakSub.Matches(_ => { })); // different delegate should not match
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void WeakSubscription_Invoke_ReturnsTrue_WhenTargetAlive()
    {
        // Arrange
        bool called = false;
        var strongRef = new object();
        Action<BeginPipelineEvent> action = e => { called = true; _ = strongRef; };
        var weakSub = new WeakSubscription<BeginPipelineEvent>(action);

        // Act
        bool result = weakSub.Invoke(_event);

        // Assert
        Assert.True(result);
        Assert.True(called);
        Assert.True(weakSub.IsAlive);
    }

    [Fact]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenTargetCollected()
    {
        WeakSubscription<BeginPipelineEvent> weakSub;

        void CreateWeak()
        {
            var target = new Target(); // strong reference
            weakSub = new WeakSubscription<BeginPipelineEvent>(target.Handler);
        }

        CreateWeak();

        // Force GC after target goes out of scope
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool result = weakSub.Invoke(_event);

        Assert.False(result);
        Assert.False(weakSub.IsAlive);
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Publish_Returns_WhenNoSubscribers()
    {
        // Arrange
        var hub = new Hub();

        var evt = _event;

        // Act & Assert: no exception, should hit the "return" branch
        hub.Publish(new UnsubscribedEvent());

        // No assertion needed; just ensuring code path executes
    }


    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_Returns_WhenNoSubscribersExist()
    {
        // Arrange
        var hub = new Hub();

        // Act: try to unsubscribe a handler that was never added
        hub.Unsubscribe<UnsubscribedEvent>(_ => { });

        // Assert: just ensure no exception is thrown
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_RemovesMatchingSubscription()
    {
        // Arrange
        var hub = new Hub();
        bool invoked = false;

        void Handler(TestEvent e) => invoked = true;

        // Subscribe
        hub.Subscribe<TestEvent>(Handler);

        // Act: unsubscribe the same handler
        hub.Unsubscribe<TestEvent>(Handler);

        // Publish event, handler should not be invoked
        hub.Publish(new TestEvent());

        // Assert
        Assert.False(invoked, "Handler should have been removed by Unsubscribe");
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_RemovesMatchingSubscription_LambdaCovered()
    {
        // Arrange
        var hub = new Hub();
        bool invoked = false;

        // Create a subscription
        void Handler(TestEvent e) => invoked = true;

        // Subscribe the handler
        hub.Subscribe<TestEvent>(Handler);

        // Access the private _subscriptions dictionary via reflection
        var subsField = typeof(Hub).GetField("_subscriptions", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;
    
        // Ensure initially one subscription
        Assert.Single(dict[typeof(TestEvent)]);

        // Act: Unsubscribe the same handler
        hub.Unsubscribe<TestEvent>(Handler);

        // Assert: subscription list is empty
        Assert.Empty(dict[typeof(TestEvent)]);

        // Publish event to ensure handler is not invoked
        hub.Publish(new TestEvent());
        Assert.False(invoked, "Handler should not be invoked after unsubscribe");
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Unsubscribe_IgnoresNonSubscriptionObjects()
    {
        // Arrange
        var hub = new Hub();

        // Access _subscriptions via reflection
        var subsField = typeof(Hub).GetField("_subscriptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        // Add a "dummy" object to the subscription list
        var dummy = new object();
        dict.GetOrAdd(typeof(TestEvent), _ => new List<object>()).Add(dummy);

        // Act: call Unsubscribe
        hub.Unsubscribe<TestEvent>(_ => { });

        // Assert: dummy object is still there (return false path executed)
        Assert.Contains(dummy, dict[typeof(TestEvent)]);
    }

    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ProxyEvent_OperatorPlusAndMinus_ShouldSubscribeAndUnsubscribe()
    {
        // Arrange
        var hub = new Hub();
        var called = false;

        void Handler(BeginPipelineEvent e)
        {
            called = true;
        }

        var proxy = hub.BeginPipelineEvent;

        // Act: subscribe using +=
        proxy += Handler;

        // Publish to ensure subscription works
        hub.Publish(_event);
    
        // Assert: handler called
        Assert.True(called);

        // Reset
        called = false;

        // Act: unsubscribe using -=
        proxy -= Handler;

        // Publish again
        hub.Publish(_event);

        // Assert: handler not called after unsubscribe
        Assert.False(called);
    }
}
