using System.Collections.Concurrent;
using System.Reflection;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Internal.EventHub;
using Hub = Ergosfare.Core.Internal.EventHub.EventHub;

namespace Ergosfare.Core.Test.EventHub;

public class EventHubTests
{
    
    
    // helper class
    private class Target
    {
        public void Handler(Hub.PreInterceptorBeingInvokeEvent e) { /* do nothing */ }
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

        hub.Subscribe<Hub.PreInterceptorBeingInvokeEvent>(_ => { });

        var subsField = typeof(Hub).GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        Assert.True(dict.ContainsKey(typeof(Hub.PreInterceptorBeingInvokeEvent)));
        Assert.Single(dict[typeof(Hub.PreInterceptorBeingInvokeEvent)]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldInvokeStrongSubscription()
    {
        var hub  = new Hub();
        var invoked = false;

        hub.Subscribe<Hub.PreInterceptorBeingInvokeEvent>(_ => invoked = true);

        hub.Publish(new Hub.PreInterceptorBeingInvokeEvent()
        {
            InterceptorName = "TestInterceptor"
        });

        Assert.True(invoked);
    }

    [Fact]
    public void Publish_ShouldRemoveDeadWeakSubscriptions()
    {
        var hub  = new Hub();

        void CreateWeak()
        {
            var temp = new object();
            hub.Subscribe<Hub.PreInterceptorBeingInvokeEvent>(_ => temp = temp.ToString(), useWeakReference: true);
        }

        CreateWeak();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var subsField = typeof(Hub).GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        var beforeCount = dict[typeof(Hub.PreInterceptorBeingInvokeEvent)].Count;

        hub.Publish(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });

        var afterCount = dict[typeof(Hub.PreInterceptorBeingInvokeEvent)].Count;

        Assert.True(afterCount <= beforeCount, "Dead weak subscriptions should be removed");
        Assert.All(dict[typeof(Hub.PreInterceptorBeingInvokeEvent)],
            s => Assert.True(((ISubscription<Hub.PreInterceptorBeingInvokeEvent>)s).IsAlive));
    }

    [Fact]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenCollected()
    {
        WeakSubscription<Hub.PreInterceptorBeingInvokeEvent> weakSub = null!;

        void CreateWeak()
        {
            var temp = new object();
            weakSub = new WeakSubscription<Hub.PreInterceptorBeingInvokeEvent>(_ => temp = temp.ToString());
        }

        CreateWeak();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var result = weakSub.Invoke(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });

        Assert.False(result);
        Assert.False(weakSub.IsAlive);
    }

    [Fact]
    public void StrongSubscription_Invoke_ReturnsTrue()
    {
        var called = false;
        var strongSub = new StrongSubscription<Hub.PreInterceptorBeingInvokeEvent>(_ => called = true);

        var result = strongSub.Invoke(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });

        Assert.True(result);
        Assert.True(called);
        Assert.True(strongSub.IsAlive);
    }

    [Fact]
    public void StrongSubscription_Dispose_DoesNotThrow()
    {
        var strongSub = new StrongSubscription<Hub.PreInterceptorBeingInvokeEvent>(_ => { });
        strongSub.Dispose();
    }

    [Fact]
    public void WeakSubscription_Dispose_DoesNotThrow()
    {
        var weakSub = new WeakSubscription<Hub.PreInterceptorBeingInvokeEvent>(_ => { });
        weakSub.Dispose();
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void StrongSubscription_Matches_ReturnsTrueForSameAction()
    {
        // Arrange
        bool called = false;
        Action<Hub.PreInterceptorBeingInvokeEvent> action = _ => called = true;
        var strongSub = new StrongSubscription<Hub.PreInterceptorBeingInvokeEvent>(action);

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
        Action<Hub.PreInterceptorBeingInvokeEvent> action = _ => called = true;
        var weakSub = new WeakSubscription<Hub.PreInterceptorBeingInvokeEvent>(action);

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
        Action<Hub.PreInterceptorBeingInvokeEvent> action = e => { called = true; _ = strongRef; };
        var weakSub = new WeakSubscription<Hub.PreInterceptorBeingInvokeEvent>(action);

        // Act
        bool result = weakSub.Invoke(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });

        // Assert
        Assert.True(result);
        Assert.True(called);
        Assert.True(weakSub.IsAlive);
    }

    [Fact]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenTargetCollected()
    {
        WeakSubscription<Hub.PreInterceptorBeingInvokeEvent> weakSub;

        void CreateWeak()
        {
            var target = new Target(); // strong reference
            weakSub = new WeakSubscription<Hub.PreInterceptorBeingInvokeEvent>(target.Handler);
        }

        CreateWeak();

        // Force GC after target goes out of scope
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool result = weakSub.Invoke(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });

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

        var evt = new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        };

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

        void Handler(Hub.PreInterceptorBeingInvokeEvent e)
        {
            called = true;
        }

        var proxy = hub.PreInterceptorBeingInvokeEventProxy;

        // Act: subscribe using +=
        proxy += Handler;

        // Publish to ensure subscription works
        hub.Publish(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });
    
        // Assert: handler called
        Assert.True(called);

        // Reset
        called = false;

        // Act: unsubscribe using -=
        proxy -= Handler;

        // Publish again
        hub.Publish(new Hub.PreInterceptorBeingInvokeEvent() {
            InterceptorName = "TestInterceptor"
        });

        // Assert: handler not called after unsubscribe
        Assert.False(called);
    }
}
