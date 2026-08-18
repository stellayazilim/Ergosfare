using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Registry;

namespace Stella.Ergosfare.Core.Internal.Factories;

/// <summary>
/// Builds a message type's participants from the composition this container selected, and
/// keeps each result.
/// </summary>
/// <param name="serviceProvider">The container this factory belongs to.</param>
/// <remarks>
/// <para>
/// The participants it returns hold no scope: instances are resolved per dispatch from the
/// provider the dispatcher passes in, so registered lifetimes apply. A pipeline whose
/// participants are all singletons — or every pipeline, when
/// <see cref="ErgosfareRuntimeOptions.MemoizeAllHandlers"/> is on — instead resolves once
/// against the root provider and keeps the instances.
/// </para>
/// <para>
/// Nothing here can go stale: the composition table is compiled and the selection is
/// complete before the container is built, so the first build for a (message type, group
/// set) is also the last.
/// </para>
/// </remarks>
internal sealed class MessageDependenciesFactory(IServiceProvider serviceProvider) : IMessageDependenciesFactory
{
    private FrozenCompositionCatalog? _compositions;
    private HandlerLifetimeRegistry? _handlerLifetimes;
    private ErgosfareRuntimeOptions? _runtimeOptions;
    private IServiceProvider? _memoizedGraphProvider;
    private IServiceProviderIsService? _resolvabilityProbe;
    private bool _servicesResolved;

    /// <summary>
    /// Participants built without groups — the common case — keyed by message type alone.
    /// </summary>
    private readonly ConcurrentDictionary<Type, IMessageDependencies> _byType = new();

    /// <summary>
    /// Participants built for a group set, keyed by message type and that set.
    /// </summary>
    private readonly ConcurrentDictionary<GroupedDependenciesKey, IMessageDependencies> _byTypeAndGroups = new();

    /// <summary>
    /// Reports whether <paramref name="handlerType"/> was registered in the module's own
    /// plain transient shape, which is what lets a generated plan construct it directly.
    /// </summary>
    /// <param name="handlerType">The participant type to ask about.</param>
    /// <returns>
    /// <c>true</c> when the registration is that shape. Always <c>false</c> until the first
    /// <see cref="Create"/> has resolved this factory's services, which is fine because
    /// executors only ask after building their participants.
    /// </returns>
    internal bool IsPlainTransientRegistration(Type handlerType)
        => _servicesResolved && (_handlerLifetimes?.IsPlainTransientRegistration(handlerType) ?? false);

    /// <inheritdoc />
    public IMessageDependencies Create(Type messageType, IEnumerable<string> groups)
        => Find(messageType, groups) ?? throw new NoHandlerFoundException(messageType);

    /// <inheritdoc />
    public IMessageDependencies? Find(Type messageType, IEnumerable<string> groups)
    {
        var groupsArray = groups as string[] ?? groups.ToArray();

        if (groupsArray.Length == 0)
        {
            if (_byType.TryGetValue(messageType, out var cached))
            {
                return cached;
            }

            // Misses are not recorded — a dictionary cannot hold one — and they cost little:
            // the lookup behind this is the catalog's own per-type cache, a dictionary hit
            // either way.
            var built = Build(messageType, groupsArray);

            return built is null ? null : _byType.GetOrAdd(messageType, built);
        }

        var key = new GroupedDependenciesKey(messageType, groupsArray);

        if (_byTypeAndGroups.TryGetValue(key, out var groupedCached))
        {
            return groupedCached;
        }

        var groupedBuilt = Build(messageType, groupsArray);

        return groupedBuilt is null ? null : _byTypeAndGroups.GetOrAdd(key, groupedBuilt);
    }

    /// <summary>
    /// Builds the participants of a message type and group set.
    /// </summary>
    /// <param name="messageType">The message type to build for.</param>
    /// <param name="groups">The groups to filter participants by.</param>
    /// <returns>The participants, or <c>null</c> when no composition serves the type.</returns>
    /// <exception cref="UnresolvableParticipantException">
    /// The composition names a participant this container cannot resolve.
    /// </exception>
    private IMessageDependencies? Build(Type messageType, string[] groups)
    {
        EnsureServices();

        // No composition serves this type, and none serves an ancestor either. What that
        // means is the caller's to decide: a dispatch fails, a publish simply has no
        // subscribers.
        if (_compositions?.Find(messageType) is not { } composition)
        {
            return null;
        }

        var shape = composition.BuildShape(messageType, groups);

        // Checked here rather than at registration: the composition table is process-wide
        // while containers are not, so only a pipeline being built inside a container can
        // say whether its participants resolve. Nothing has been cached at this point, so a
        // failure here does not stick.
        EnsureParticipantsResolvable(shape, messageType, _resolvabilityProbe);

        // Demanded memoization bars a compiled plan; the all-singleton kind does not —
        // resolving a singleton per dispatch returns the one instance anyway, so the plan
        // and the memoized references cannot be told apart. The dependencies carry which
        // kind this is so the executors refuse only what genuinely conflicts.
        var forcedMemoization = _runtimeOptions?.MemoizeAllHandlers ?? false;
        var memoizeInstances = forcedMemoization
                               || (_handlerLifetimes?.AreAllParticipantsSingleton(messageType, shape) ?? false);

        return new MessageDependencies(
            shape, memoizeInstances ? _memoizedGraphProvider ?? serviceProvider : null, forcedMemoization);
    }

    /// <summary>
    /// Resolves the container services this factory reads, once.
    /// </summary>
    /// <remarks>
    /// Deferred rather than done in the constructor because the factory is itself resolved
    /// while the container is still being built.
    /// </remarks>
    private void EnsureServices()
    {
        if (_servicesResolved)
        {
            return;
        }

        _compositions = serviceProvider.GetService<FrozenCompositionCatalog>();
        _handlerLifetimes = serviceProvider.GetService<HandlerLifetimeRegistry>();
        _runtimeOptions = serviceProvider.GetService<ErgosfareRuntimeOptions>();
        _memoizedGraphProvider = serviceProvider.GetService<RootServiceProviderAccessor>()?.RootProvider ?? serviceProvider;
        _resolvabilityProbe = serviceProvider.GetService<IServiceProviderIsService>();
        _servicesResolved = true;
    }

    /// <summary>
    /// Checks that the container can build every participant of a pipeline, before any of
    /// them is asked for.
    /// </summary>
    /// <param name="shape">The pipeline to check.</param>
    /// <param name="messageType">The message type the pipeline belongs to.</param>
    /// <param name="probe">
    /// The container's registration probe, or <c>null</c> when it offers none.
    /// </param>
    /// <exception cref="UnresolvableParticipantException">
    /// A participant is not registered with the container.
    /// </exception>
    /// <remarks>
    /// The probe answers from the registrations without constructing anything; actually
    /// resolving each participant would instantiate the whole pipeline just to check it. A
    /// container that offers no probe is left alone, and its participants fail when they
    /// are first resolved instead.
    /// </remarks>
    private static void EnsureParticipantsResolvable(
        FrozenPipelineShape shape, Type messageType, IServiceProviderIsService? probe)
    {
        if (probe is null)
        {
            return;
        }

        Check(shape.Handlers, messageType, probe);
        Check(shape.IndirectHandlers, messageType, probe);
        Check(shape.PreInterceptors, messageType, probe);
        Check(shape.PostInterceptors, messageType, probe);
        Check(shape.ExceptionInterceptors, messageType, probe);
        Check(shape.FinalInterceptors, messageType, probe);

        static void Check(IReadOnlyList<Type> participants, Type messageType, IServiceProviderIsService probe)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                if (!probe.IsService(participants[i]))
                {
                    throw new UnresolvableParticipantException(messageType, participants[i]);
                }
            }
        }
    }
}
