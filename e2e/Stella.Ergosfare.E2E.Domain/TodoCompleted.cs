namespace Stella.Ergosfare.E2E.Domain;

/// <summary>
/// A domain event as a plain POCO — note it does NOT implement Ergosfare's <c>IEvent</c>,
/// and this Domain assembly has no Ergosfare reference at all. The event module accepts any
/// POCO, so it is published with the generic <c>PublishAsync&lt;T&gt;</c> overload and picked
/// up by a handler registered against this type.
/// </summary>
public sealed record TodoCompleted(Guid Id);
