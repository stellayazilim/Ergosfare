using Stella.Ergosfare.Core;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// <see cref="CommandMediator"/> with exactly one constructor, which is what the container
/// registers.
/// </summary>
/// <param name="engine">The container's dispatch engine.</param>
/// <param name="serviceProvider">The provider of the scope being served.</param>
/// <remarks>
/// Having a single constructor lets container activation bind without choosing between
/// overloads, and without the per-resolution lookup a factory registration would cost.
/// Resolving <see cref="Stella.Ergosfare.Commands.Abstractions.ICommandMediator"/> builds
/// this one object; the engine behind it is shared across the process.
/// </remarks>
internal sealed class EngineBackedCommandMediator(MessageDispatchEngine engine, IServiceProvider serviceProvider)
    : CommandMediator(engine, serviceProvider);
