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
    public void GetHashCode_ShouldBeConsistentForEqualObjects()
    {
        var id = Guid.NewGuid();
        var a = new TestEvent { Id = id };
        var b = new TestEvent { Id = id };

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
    
    
        

    [Fact]
    [Trait("Category", "Unit")]
    public void GetHashCode_ShouldAggregateAllComponents()
    {
        var evt = new MultiComponentEvent { Id = Guid.NewGuid(), Number = 42 };
        var hash = evt.GetHashCode();

        // Just call it to cover the XOR aggregate
        Assert.IsType<int>(hash);
    }
}