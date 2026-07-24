# Stella.Ergosfare.Events

Event module runtime of [Ergosfare](https://github.com/stellayazilim/Ergosfare): the
`IEventMediator` implementation broadcasting events sequentially to every registered
handler — the event's own handlers first, then covariantly matched ones (registered
against a base type or interface) — through cached, reflection-free invokers closed over
each event's concrete type.

Applications should reference the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package;
libraries that only declare events need
[`Stella.Ergosfare.Events.Abstractions`](https://www.nuget.org/packages/Stella.Ergosfare.Events.Abstractions).
