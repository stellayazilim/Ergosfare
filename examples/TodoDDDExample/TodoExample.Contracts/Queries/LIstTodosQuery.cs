using Ergosfare.Contracts;
using TodoExample.Domain;

namespace TodoExample.Contracts.Queries;

public record ListTodosQuery : IQuery<List<Todo>>;