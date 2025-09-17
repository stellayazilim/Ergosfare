using Ergosfare.Core.Abstractions.SignalHub.Signals;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.SignalHub;

/// <summary>
/// Provides a base class for tests that require access to a shared <see cref="SignalHubFixture"/>.
/// </summary>
/// <param name="signalHubFixture">
/// The <see cref="SignalHubFixture"/> instance provided by xUnit's <c>IClassFixture</c>.
/// </param>
public abstract class BaseSignalFixture(SignalHubFixture signalHubFixture) : IClassFixture<SignalHubFixture>
{
    
    protected  (Action<BeginPipelineSignal>, bool) CreateStubAction(object reference) {
        bool called = false;
        return (e =>
        {
            called = true;
            _ = reference;
        }, called);
    }
    
    /// <summary>
    /// Gets the shared <see cref="SignalHubFixture"/> instance for use in derived test classes.
    /// </summary>
    protected SignalHubFixture SignalHubFixture = signalHubFixture;

    // ReSharper disable once InconsistentNaming
    protected readonly BeginPipelineSignal Signal = signalHubFixture.BeginPipelineEvent<StubMessage>();
}