
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Represents a simple stub event used for testing non-generic event handling.
/// Owned by <see cref="BroadcastPipelineTests"/>: the compiled broadcast plan bakes every
/// discoverable subscriber of an event, so a message type is never shared between test
/// classes — each class registers its event's full compiled pipeline.
/// </summary>
public record StubNonGenericEvent: IEvent;

/// <summary>
/// Represents a stub event designed to throw an exception when handled,
/// used for testing event exception handling mechanisms.
/// </summary>
public record StubNonGenericEventThrows: IEvent;