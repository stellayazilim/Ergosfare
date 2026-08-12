using Stella.Ergosfare.Core;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// DI-facing shape of <see cref="QueryMediator"/> with exactly one constructor, so
/// container activation binds the engine-backed path deterministically — no
/// multi-constructor ambiguity, and no per-resolution service lookup a factory
/// registration would pay. Resolving <see cref="Stella.Ergosfare.Queries.Abstractions.IQueryMediator"/>
/// builds this single object; the engine itself is a process-wide singleton.
/// </summary>
internal sealed class EngineBackedQueryMediator(
    MessageDispatchEngine engine,
    IServiceProvider serviceProvider)
    : QueryMediator(engine, serviceProvider);
