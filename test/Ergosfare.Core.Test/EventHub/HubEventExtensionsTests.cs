using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Events;
using Ergosfare.Core.Extensions;
namespace Ergosfare.Core.Test.EventHub;

public class HubEventExtensionsTests
{

    private sealed class TestEvent: PipelineEvent
    {
        public string Data { get; init; } 
        
        public override IEnumerable<object> GetEqualityComponents()
        {
            yield return Data;
        }
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldPublishEventOnInstances()
    {
        // arrange
        var @event = new TestEvent
        {
            Message = "string",
            Result = null,
            Data = "data",
        };

      
        
        // assert
        PipelineEvent.Subscribe<TestEvent>(e =>
        {
            Assert.Equal(@event.Data, e.Data);
        });
        
        
        // act
        @event.Invoke();

    }
}