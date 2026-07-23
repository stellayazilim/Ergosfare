# Stella.Ergosfare.Queries.Abstractions

Query contracts of [Ergosfare](https://github.com/stellayazilim/Ergosfare): `IQuery<TResult>`,
`IStreamQuery<TResult>`, `IQueryHandler<,>`, `IStreamQueryHandler<,>`, the query-flavored
interceptor interfaces, and `IQueryMediator` — including `IAsyncEnumerable` streaming and
the external-context overload for nested dispatch (`QueryAsync(query, scope.Context)`).

Reference this from **libraries that declare queries or query handlers**. Applications
should reference the [`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare)
meta package instead.
