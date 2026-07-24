# Stella.Ergosfare.Commands

Command module runtime of [Ergosfare](https://github.com/stellayazilim/Ergosfare): the
`ICommandMediator` implementation dispatching commands through cached, reflection-free
pipeline executors closed over each command's concrete type.

Applications should reference the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package;
libraries that only declare commands need
[`Stella.Ergosfare.Commands.Abstractions`](https://www.nuget.org/packages/Stella.Ergosfare.Commands.Abstractions).
