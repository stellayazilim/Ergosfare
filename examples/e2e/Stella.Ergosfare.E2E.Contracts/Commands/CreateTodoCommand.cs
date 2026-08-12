using Stella.Ergosfare.Commands.Abstractions;

namespace Stella.Ergosfare.E2E.Contracts.Commands;

/// <summary>Creates a todo and returns its generated id.</summary>
public sealed record CreateTodoCommand(string Title) : ICommand<Guid>;
