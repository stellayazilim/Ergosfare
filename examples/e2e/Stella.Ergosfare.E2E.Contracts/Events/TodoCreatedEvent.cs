using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.E2E.Contracts.Events;

/// <summary>Broadcast after a todo is created; drives the created-count statistic.</summary>
public sealed record TodoCreatedEvent(Guid Id, string Title) : IEvent;
