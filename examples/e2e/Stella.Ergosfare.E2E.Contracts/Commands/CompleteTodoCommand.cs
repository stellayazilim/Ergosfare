using Stella.Ergosfare.Commands.Abstractions;

namespace Stella.Ergosfare.E2E.Contracts.Commands;

/// <summary>Marks an existing todo complete. Produces no result.</summary>
public sealed record CompleteTodoCommand(Guid Id) : ICommand;
