using Ergosfare.Contracts;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Test;

public record StubNonGenericEvent: IEvent;
public record StubNonGenericEventThrows: IEvent;