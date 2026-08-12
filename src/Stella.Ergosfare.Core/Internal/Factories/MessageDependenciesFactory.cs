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
/// Creates (and caches per container) the resolved handler graph for a message type,
/// reading the composition from the container's <see cref="FrozenCompositionCatalog"/>.
/// </summary>
/// <remarks>
/// Dependencies are provider-independent: handler instances are resolved per invocation
/// from the dispatching scope's provider (carried by the execution context), so DI
/// lifetimes are honored without binding the graph to any scope. Messages whose pipeline
/// is fully singleton-registered — or all messages when
/// <see cref="ErgosfareRuntimeOptions.MemoizeAllHandlers"/> is enabled — additionally
/// cache resolved instances inside their references, pinned to the root provider.
/// <para>
/// A composition is settled before the container is built — the table is compiled and the
/// selection is registration — so a built graph never goes stale and the cache needs no
/// version stamp: the first build for a (message type, group set) is the last.
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
    /// Group-less graphs, the overwhelmingly common case, keyed by message type alone.
    /// </summary>
    private readonly ConcurrentDictionary<Type, IMessageDependencies> _byType = new();

    /// <summary>Grouped graphs, keyed by message type and the requested group set.</summary>
    private readonly ConcurrentDictionary<GroupedDependenciesKey, IMessageDependencies> _byTypeAndGroups = new();

    /// <summary>
    /// Whether the handler type's effective DI registration is the module's own plain
    /// transient shape, making container resolution and direct construction semantically
    /// identical; see <see cref="HandlerLifetimeRegistry.IsPlainTransientRegistration"/>.
    /// Always <c>false</c> before the first <see cref="Create"/> resolves the services —
    /// executors only consult this after building their dependencies.
    /// </summary>
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

            // A miss is not cached: the dictionary cannot hold one, and the lookup behind
            // it is the catalog's own per-type cache — a dictionary hit either way.
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

    private IMessageDependencies? Build(Type messageType, string[] groups)
    {
        EnsureServices();

        // The one deliberately remaining corner: a message no compiled composition serves,
        // and whose ancestors none serves either. Callers decide what that means — a
        // dispatch fails, a publish simply has no subscribers.
        if (_compositions?.Find(messageType) is not { } composition)
        {
            return null;
        }

        var shape = composition.BuildShape(messageType, groups);

        // Fail fast here rather than during registration: the table is process-wide while
        // containers are per-application, so only a pipeline being built in a container's
        // context can answer whether its participants are resolvable. Nothing is cached
        // before this returns, so the failure is not sticky.
        EnsureParticipantsResolvable(shape, messageType, _resolvabilityProbe);

        // Pipelines that are fully singleton-registered (or forced via MemoizeAllHandlers)
        // cache handler instances inside their references, pinned to the root provider.
        var memoizeInstances = (_runtimeOptions?.MemoizeAllHandlers ?? false)
                               || (_handlerLifetimes?.AreAllParticipantsSingleton(messageType, shape) ?? false);

        return new MessageDependencies(
            shape, memoizeInstances ? _memoizedGraphProvider ?? serviceProvider : null);
    }

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
    /// Verifies every participant of the shape is something the container knows how to
    /// build, before any of them is asked for.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="IServiceProviderIsService"/>, which answers from the registrations
    /// without constructing anything — a plain resolution attempt would instantiate the
    /// whole pipeline on every rebuild. Containers that do not offer it are left alone:
    /// the participant then fails at resolution time, exactly as before this check
    /// existed.
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
