# Stella.Ergosfare.Queries

Query module runtime of [Ergosfare](https://github.com/stellayazilim/Ergosfare): the
`IQueryMediator` implementation for single-result queries and `IAsyncEnumerable` stream
queries, dispatching through cached, reflection-free pipelines closed over each query's
concrete type.

Applications should reference the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package;
libraries that only declare queries need
[`Stella.Ergosfare.Queries.Abstractions`](https://www.nuget.org/packages/Stella.Ergosfare.Queries.Abstractions).
