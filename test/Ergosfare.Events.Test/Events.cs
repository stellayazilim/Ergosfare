using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Test;

/// <summary>
/// Represents a simple stub event used for testing non-generic event handling.
/// </summary>
public record StubNonGenericEvent: IEvent;

/// <summary>
/// Represents a stub event designed to throw an exception when handled,
/// used for testing event exception handling mechanisms.
/// </summary>
public record StubNonGenericEventThrows: IEvent;