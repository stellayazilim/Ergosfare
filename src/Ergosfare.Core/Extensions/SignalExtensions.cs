using Ergosfare.Core.Abstractions.SignalHub;

namespace Ergosfare.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Signal"/> objects.
/// </summary>
public static class SignalExtensions
{
    /// <summary>
    /// Publishes the specified signal to the global <see cref="SignalHub"/> via the <see cref="SignalHubAccessor"/> singleton.
    /// </summary>
    /// <param name="signal">The signal instance to invoke.</param>
    public static void Invoke(this Signal signal)
    {
        SignalHubAccessor.Instance.Publish(signal);
    }
}