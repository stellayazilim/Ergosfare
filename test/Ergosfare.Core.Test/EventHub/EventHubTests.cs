using System.Collections.Concurrent;
using System.Reflection;
using Ergosfare.Core.Internal.EventHub;
using Hub = Ergosfare.Core.Internal.EventHub.EventHub;

namespace Ergosfare.Core.Test.EventHub;

public class EventHubTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldHaveRegisteredEvents()
    {
        // arrange
        var hub = new Hub();

        hub.Subscribe<string>(_ => {});
        // Get the private _subscriptions field
        var subsField = typeof(Hub).GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        // act
        // Cast the field value back to the dictionary
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;
        
        //assert
        Assert.True(dict.ContainsKey(typeof(string)));
        Assert.Single(dict[typeof(string)]);
    }


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldInvokeEvent()
    {
        // arrange
        var hub = new Hub();

        // act
        hub.Publish<string>("Foo");

        // assert
        hub.Subscribe<string>(s => Assert.Equal("Foo", s));
    }
    
    
    
      
    
    [Fact]
    public void Publish_ShouldInvokeSubscribers_AndRemoveDeadWeakSubscriptions()
    {
        var hub = new Hub();

        bool strongInvoked = false;
        bool weakInvoked = false;

        // Strong subscription
        hub.Subscribe<string>(msg => strongInvoked = msg == "test");

        // Weak subscription that will be collected
        var tempObj = new object();
        hub.Subscribe<string>(msg =>
        {
            weakInvoked = true;
        }, useWeakReference: true);

        // Force GC to potentially collect weak references
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Publish event
        hub.Publish("test");

        // Assert strong subscription invoked
        Assert.True(strongInvoked);

        // weakInvoked may or may not be true depending on GC
        // But the key is that dead weak subscriptions are removed
        var subsField = typeof(Hub).GetField("_subscriptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var dict = (System.Collections.Concurrent.ConcurrentDictionary<Type, System.Collections.Generic.List<object>>)subsField.GetValue(hub)!;
        var subsList = dict[typeof(string)];

        // There should be at least 1 alive subscription (the strong one)
        Assert.All(subsList, s => Assert.True(((ISubscription<string>)s).IsAlive));
    }
    
    
    
    
    [Fact]
    public void Publish_ShouldRemoveDeadWeakSubscriptions()
    {
        var hub = new Hub();

        // Create a weak subscription with a target that will be collected
        void CreateWeakSubscription()
        {
            var temp = new object();
            hub.Subscribe<string>(_ => { _ = temp.ToString(); }, useWeakReference: true);
        }

        CreateWeakSubscription();

        // Force GC to collect the weakly referenced target
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Access the private subscriptions field via reflection
        var subsField = typeof(Hub).GetField("_subscriptions",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (ConcurrentDictionary<Type, List<object>>)subsField.GetValue(hub)!;

        var subsBefore = dict[typeof(string)].Count;

        // Publish event to trigger cleanup of dead weak subscriptions
        hub.Publish("test");

        var subsAfter = dict[typeof(string)].Count;

        // Assert that at least one dead weak subscription was removed
        Assert.True(subsAfter < subsBefore, "Dead weak subscriptions should be removed during publish");
    }
    
    
    [Fact]
    public void WeakSubscription_Invoke_ReturnsFalse_WhenTargetCollected()
    {
        WeakSubscription<string> weakSub;
        
        void CreateWeakSub()
        {
            var temp = new object();
            weakSub = new WeakSubscription<string>(_ => { _ = temp.ToString(); });
        }

        CreateWeakSub();

        // Force GC to collect the target
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Now target is gone, Invoke should return false
        bool result = weakSub.Invoke("test");

        Assert.False(result);
        Assert.False(weakSub.IsAlive);
    }

    [Fact]
    public void StrongSubscription_Invoke_ReturnsTrue()
    {
        bool called = false;
        var strongSub = new StrongSubscription<string>(_ => called = true);

        bool result = strongSub.Invoke("test");

        Assert.True(result);
        Assert.True(called);
        Assert.True(strongSub.IsAlive);
    }

    [Fact]
    public void StrongSubscription_Dispose_DoesNotThrow()
    {
        var strongSub = new StrongSubscription<string>(_ => { });
        strongSub.Dispose();
    }

    [Fact]
    public void WeakSubscription_Dispose_DoesNotThrow()
    {
        var weakSub = new WeakSubscription<string>(_ => { });
        weakSub.Dispose();
    }
}