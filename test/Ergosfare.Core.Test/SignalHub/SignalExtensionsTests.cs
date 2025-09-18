using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Core.Extensions;
using Ergosfare.Test.Fixtures;

namespace Ergosfare.Core.Test.SignalHub;

/// <summary>
/// Unit tests for <see cref="SignalHub"/> extension methods.
/// </summary>
/// <remarks>
/// These tests validate that hub events published through their
/// extension methods (such as <c>Invoke()</c>) are correctly
/// propagated to subscribers. Ensures that the event hub integration
/// and extension helpers behave as expected.
/// </remarks>
public class SignalExtensionsTests(
    SignalFixture signalHubFixture) : BaseSignalFixture(signalHubFixture)
{
    
    /// <summary>
    /// Verifies that publishing a <see cref="BeginPipelineSignal"/> through its
    /// extension <c>Invoke()</c> method correctly triggers subscribed handlers.
    /// </summary>
    /// <remarks>
    /// This test subscribes to the <see cref="PipelineSignal"/> for
    /// <see cref="BeginPipelineSignal"/> and asserts that the published
    /// predefined <c>@event</c> instance is the same object received
    /// in the subscriber callback. Ensures the event system delivers the exact
    /// instance to subscribers.
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldPublishEventOnInstances()
    {
        // arrange
        SignalHubFixture = SignalHubFixture.New;
        
        // subscribe the event before publishing
        PipelineSignal.Subscribe<BeginPipelineSignal>(e =>
        {
            // assert
            Assert.Same(Signal, e);
        });
    
        // act
        // publish predefined event
        Signal.Invoke();

        // cleanup
        SignalHubFixture.Dispose();
    }
}