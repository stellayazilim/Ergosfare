using Stella.Ergosfare.Core;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// DI-facing shape of <see cref="CommandMediator"/> with exactly one constructor, so
/// container activation binds the engine-backed path deterministically — no
/// multi-constructor ambiguity, and no per-resolution service lookup a factory
/// registration would pay. Resolving <see cref="Stella.Ergosfare.Commands.Abstractions.ICommandMediator"/>
/// builds this single object; the engine itself is a process-wide singleton.
/// </summary>
internal sealed class EngineBackedCommandMediator(MessageDispatchEngine engine, IServiceProvider serviceProvider)
    : CommandMediator(engine, serviceProvider);
