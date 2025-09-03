using Ergosfare.Core.Abstractions.EventHub;

namespace Ergosfare.Core.EventHub;
/// <summary>
/// Provides access to a global singleton instance of <see cref="IEventHub"/>.
/// Ensures only one hub exists across multiple module registrations.
/// </summary>
internal static class EventHubAccessor
{
    /// <summary>
    /// Lazy initializer ensures thread-safe initialization.
    /// </summary>
    private static readonly Lazy<IEventHub> LazyInstance =
        new(() => new Core.EventHub.EventHub(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the event hub.
    /// Created on first access in a thread-safe manner.
    /// </summary>
    public static IEventHub Instance => LazyInstance.Value;
}