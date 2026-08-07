using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// DI-facing shape of <see cref="EventMediator"/> with exactly one constructor, so
/// container activation binds the engine-backed path deterministically — no
/// multi-constructor ambiguity, and no per-resolution service lookup a factory
/// registration would pay. Resolving <see cref="Stella.Ergosfare.Events.Abstractions.IEventMediator"/>
/// or <see cref="Stella.Ergosfare.Events.Abstractions.IPublisher"/> builds this single
/// object; the engine itself is a process-wide singleton.
/// </summary>
internal sealed class EngineBackedEventMediator(
    MessageDispatchEngine engine,
    IServiceProvider serviceProvider,
    ActualTypeOrFirstAssignableTypeMessageResolveStrategy messageResolveStrategy,
    IResultAdapterService? resultAdapterService)
    : EventMediator(engine, serviceProvider, messageResolveStrategy, resultAdapterService);
