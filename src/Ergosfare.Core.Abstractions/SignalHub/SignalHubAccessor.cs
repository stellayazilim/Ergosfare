using System;
using System.Threading;

namespace Ergosfare.Core.Abstractions.SignalHub;
/// <summary>
/// Provides access to a global singleton instance of <see cref="ISignalHub"/>.
/// Ensures only one hub exists across multiple module registrations.
/// </summary>
public static class SignalHubAccessor
{
    private static readonly Lock Sync = new();    /// <summary>
    /// Lazy initializer ensures thread-safe initialization.
    /// </summary>
    private static Lazy<ISignalHub> _lazyInstance =
        new(() => new Abstractions.SignalHub.SignalHub(), LazyThreadSafetyMode.ExecutionAndPublication);
    
    /// <summary>
    /// Gets the singleton instance of the event hub.
    /// Created on first access in a thread-safe manner.
    /// </summary>
    public static ISignalHub Instance => _lazyInstance.Value;

    /// <summary>
    /// Resets the signal hub instance. Intended for test scenarios
    /// to guarantee a clean state between test runs.
    /// </summary>
    internal static void ResetInstance()
    {
        lock (Sync)
        {
            _lazyInstance = new(() => new Abstractions.SignalHub.SignalHub(), LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}