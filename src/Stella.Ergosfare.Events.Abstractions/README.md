# Stella.Ergosfare.Events.Abstractions

Event contracts of [Ergosfare](https://github.com/stellayazilim/Ergosfare): `IEvent`,
`IEventHandler<>`, the event-flavored interceptor interfaces, and `IEventMediator` —
including the external-context overload for nested dispatch
(`PublishAsync(@event, scope.Context)`).

Reference this from **libraries that declare events or event handlers**. Applications
should reference the [`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare)
meta package instead.
