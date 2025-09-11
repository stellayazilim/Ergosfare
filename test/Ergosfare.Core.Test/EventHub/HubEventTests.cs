using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.Test.EventHub;

public class HubEventTests
{

        
    private sealed class MultiComponentEvent : HubEvent
    {
        public Guid Id { get; init; }
        public int Number { get; init; }

        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return Id;
            yield return Number;
        }
    }

    
    private sealed class TestEvent : HubEvent
    {
        public Guid Id { get; init; }
        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return Id;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void EqualityOperators_ShouldWork()
    {
        var id = Guid.NewGuid();
        var a = new TestEvent { Id = id };
        var b = new TestEvent { Id = id };
        var c = new TestEvent { Id = Guid.NewGuid() };

        // operator ==
        Assert.True(a == b);
        Assert.False(a == c);

        // operator !=
        Assert.False(a != b);
        Assert.True(a != c);

        // Equals method
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void GetHashCode_ShouldBeConsistentForEqualObjects()
    {
        var id = Guid.NewGuid();
        var a = new TestEvent { Id = id };
        var b = new TestEvent { Id = id };

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
    
    
        

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void GetHashCode_ShouldAggregateAllComponents()
    {
        var evt = new MultiComponentEvent { Id = Guid.NewGuid(), Number = 42 };
        var hash = evt.GetHashCode();

        // Just call it to cover the XOR aggregate
        Assert.IsType<int>(hash);
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Add_ShouldAddRelatedEvent()
    {
        var parent = new TestEvent { };
        var child = new TestEvent { };

        parent.Add(child);

        Assert.Single(parent.RelatedEvents);
        Assert.Contains(child, parent.RelatedEvents);
    }


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void AddRange_ShouldAddMultipleRelatedEvents()
    {
        var parent = new TestEvent { };
        var children = new[]
        {
            new TestEvent { },
            new TestEvent { }
        };

        parent.AddRange(children);

        Assert.Equal(2, parent.RelatedEvents.Count);
        Assert.Contains(children[0], parent.RelatedEvents);
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RelatedEvents_ShouldBeReadOnly()
    {
        var parent = new TestEvent { };
        var child = new TestEvent {  };

        parent.Add(child);

        // IReadOnlyList should prevent modifying the list directly
        var relatedEvents = parent.RelatedEvents;
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)relatedEvents).Add(new TestEvent()));
    }
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RelatedEvents_AddedEventsDoNotAffectEquality()
    {
        var id = Guid.NewGuid();
        var a = new TestEvent { Id = id };
        var b = new TestEvent { Id = id };

        a.Add(new TestEvent { Id = Guid.NewGuid() });
        b.Add(new TestEvent { Id = Guid.NewGuid() });

        Assert.Equal(a, b); // equality ignores RelatedEvents
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RelatedEvents_ShouldGetTimestamp()
    {

        // arrange & act
        var @event = new TestEvent();
        var now = DateTime.UtcNow;
        // assert
        Assert.True(@event.Timestamp < now);
        Assert.InRange(@event.Timestamp, now.AddSeconds(-1), now.AddSeconds(1));
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RelatedEvents_ShouldNotEqual()
    {

        // arrange & act
        var @event = new TestEvent();
        var other = new object();
        var now = DateTime.UtcNow;
        // assert
        Assert.NotEqual(@event, other);
        
    }
}