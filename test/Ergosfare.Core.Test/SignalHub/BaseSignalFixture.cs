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
public abstract class BaseSignalFixture(SignalFixture signalHubFixture) : IClassFixture<SignalFixture>
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
    /// Creates a new <see cref="SignalFixture.StubSignal"/> using the provided values.
    /// This helper method delegates to <see cref="SignalFixture.CreateSignal"/> 
    /// to simplify signal creation within derived test classes.
    /// </summary>
    /// <param name="v1">The integer value used as the first equality component.</param>
    /// <param name="v2">The string value used as the second equality component.</param>
    /// <returns>
    /// A new instance of <see cref="SignalFixture.StubSignal"/> initialized with the specified values.
    /// </returns>
    protected SignalFixture.StubSignal CreateSignal(int v1, string v2) => SignalHubFixture.CreateSignal(v1, v2);
    
    /// <summary>
    /// Gets the shared <see cref="SignalHubFixture"/> instance for use in derived test classes.
    /// </summary>
    protected SignalFixture SignalHubFixture = signalHubFixture;

    // ReSharper disable once InconsistentNaming
    protected readonly BeginPipelineSignal Signal = signalHubFixture.BeginPipelineEvent<StubMessage>();
}