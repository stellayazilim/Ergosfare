# Stella.Ergosfare.Commands.Abstractions

Command contracts of [Ergosfare](https://github.com/stellayazilim/Ergosfare): `ICommand`,
`ICommand<TResult>`, `ICommandHandler<,>`, the command-flavored pre/post/exception/final
interceptor interfaces, and `ICommandMediator` — including the external-context overloads
for nested dispatch (`SendAsync(command, scope.Context)`).

Reference this from **libraries that declare commands or command handlers**. Applications
should reference the [`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare)
meta package instead.
