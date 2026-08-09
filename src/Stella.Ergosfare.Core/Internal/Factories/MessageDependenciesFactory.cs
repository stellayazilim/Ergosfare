using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Caching;
using Stella.Ergosfare.Core.Internal.Registry;

namespace Stella.Ergosfare.Core.Internal.Factories;

/// <summary>
/// Creates (and caches process-wide) the resolved handler graph for a message type.
/// </summary>
/// <remarks>
/// Dependencies are provider-independent: handler instances are resolved per invocation
/// from the dispatching scope's provider (carried by the execution context), so DI
/// lifetimes are honored without binding the graph to any scope. Messages whose pipeline
/// is fully singleton-registered — or all messages when
/// <see cref="ErgosfareRuntimeOptions.MemoizeAllHandlers"/> is enabled — additionally
/// cache resolved instances inside their references, pinned to the root provider.
/// The factory itself is registered as a singleton; every dispatch after the first is a
/// cache lookup.
/// </remarks>
internal sealed class MessageDependenciesFactory(IServiceProvider serviceProvider) : IMessageDependenciesFactory
{
    private MessageDescriptorCache? _cache;
    private IMessageRegistry? _registry;
    private HandlerLifetimeRegistry? _handlerLifetimes;
    private ErgosfareRuntimeOptions? _runtimeOptions;
    private IServiceProvider? _memoizedGraphProvider;
    private IServiceProviderIsService? _resolvabilityProbe;
    private bool _servicesResolved;

    /// <summary>
    /// The current registry version, or <see cref="int.MinValue"/> before the first
    /// <see cref="Create"/> resolves the registry. Executors compare this against the
    /// version their cached dependencies were built at and skip <see cref="Create"/>
    /// entirely on a match.
    /// </summary>
    internal int CurrentRegistryVersion
        => !_servicesResolved
            ? int.MinValue
            : _registry is null
                ? int.MinValue
                : _registry is MessageRegistry typedRegistry
                    ? typedRegistry.Version
                    : _registry.Count;

    /// <summary>
    /// Whether the handler type's effective DI registration is the module's own plain
    /// transient shape, making container resolution and direct construction semantically
    /// identical; see <see cref="HandlerLifetimeRegistry.IsPlainTransientRegistration"/>.
    /// Always <c>false</c> before the first <see cref="Create"/> resolves the registry —
    /// executors only consult this after building their dependencies.
    /// </summary>
    internal bool IsPlainTransientRegistration(Type handlerType)
        => _servicesResolved && (_handlerLifetimes?.IsPlainTransientRegistration(handlerType) ?? false);

    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        var cache = _cache ??= serviceProvider.GetRequiredService<MessageDescriptorCache>();

        if (!_servicesResolved)
        {
            _registry = serviceProvider.GetService<IMessageRegistry>();
            _handlerLifetimes = serviceProvider.GetService<HandlerLifetimeRegistry>();
            _runtimeOptions = serviceProvider.GetService<ErgosfareRuntimeOptions>();
            _memoizedGraphProvider = serviceProvider.GetService<RootServiceProviderAccessor>()?.RootProvider ?? serviceProvider;
            _resolvabilityProbe = serviceProvider.GetService<IServiceProviderIsService>();
            _servicesResolved = true;
        }

        // The registry version is read ONCE, before any shape or dependency build, and
        // stamps every cache entry this call produces: a build racing a registration
        // then lands with the old version and the next reader rebuilds instead of
        // serving a stale pipeline. MessageRegistry.Version also changes when handlers
        // are added to existing messages; Count is the fallback for foreign registry
        // implementations, and 0 the constant when no registry is resolvable at all.
        var registryVersion = 0;

        if (_registry is not null)
        {
            registryVersion = _registry is MessageRegistry registry ? registry.Version : _registry.Count;
            cache.InvalidateIfRegistryChanged(registryVersion);
            _handlerLifetimes?.InvalidateIfRegistryChanged(registryVersion);
        }

        var groupsArray = groups as string[] ?? groups.ToArray();

        if (cache.TryGetDependencies(messageType, groupsArray, registryVersion, out var cached))
        {
            return cached!;
        }

        var shape = cache.GetOrAddShape(messageType, groupsArray, descriptor, registryVersion);

        // Fail fast here rather than in IMessageRegistry.Register: the registry is
        // process-wide while containers are per-application, so the same participant can
        // be resolvable in one container and absent from another — only a pipeline being
        // built in a container's context can answer. Nothing is cached before this
        // returns, so the failure is not sticky: a container that does register the
        // participant builds the same pipeline and dispatches normally.
        EnsureParticipantsResolvable(shape, messageType, _resolvabilityProbe);

        // Pipelines that are fully singleton-registered (or forced via MemoizeAllHandlers)
        // cache handler instances inside their references, pinned to the root provider.
        var memoizeInstances = (_runtimeOptions?.MemoizeAllHandlers ?? false)
                               || (_handlerLifetimes?.AreAllHandlersSingleton(messageType, descriptor) ?? false);

        var dependencies = new MessageDependencies(
            shape, memoizeInstances ? _memoizedGraphProvider ?? serviceProvider : null);

        cache.AddDependencies(messageType, groupsArray, dependencies, registryVersion);

        return dependencies;
    }

    /// <summary>
    /// Verifies every planned participant is something the container knows how to build,
    /// before any of them is asked for.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="IServiceProviderIsService"/>, which answers from the registrations
    /// without constructing anything — a plain resolution attempt would instantiate the
    /// whole pipeline on every rebuild. Containers that do not offer it are left alone:
    /// the participant then fails at resolution time, exactly as before this check
    /// existed.
    /// </remarks>
    private static void EnsureParticipantsResolvable(
        MessagePipelineShape shape, Type messageType, IServiceProviderIsService? probe)
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

        static void Check<TDescriptor>(
            PlannedHandler<TDescriptor>[] planned, Type messageType, IServiceProviderIsService probe)
            where TDescriptor : IHandlerDescriptor
        {
            for (var i = 0; i < planned.Length; i++)
            {
                if (!probe.IsService(planned[i].HandlerType))
                {
                    throw new UnresolvableParticipantException(messageType, planned[i].HandlerType);
                }
            }
        }
    }
}
