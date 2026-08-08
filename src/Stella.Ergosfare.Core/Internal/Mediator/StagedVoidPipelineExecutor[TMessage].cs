using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Internal.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Void pipeline hosting a staged plan: bespoke straight-line code for the message's whole
/// interceptor-bearing pipeline. The plan is advisory — the registry-version-guarded
/// dependency cache re-validates the plan's composition against the live pipeline
/// (<see cref="StagedPlanGate"/>) plus the memoization and adapter gates, and any mismatch
/// falls back to the runtime strategy, preserving semantics exactly. The plan resolves its
/// participants from the dispatching scope's provider — outside memoized mode that is
/// literally what the runtime handler references do, so container semantics are preserved.
/// Like every version-guarded executor, a dispatch racing a registration may run the
/// previous shape once; it never runs a shape the registry has not published.
/// </summary>
internal sealed class StagedVoidPipelineExecutor<TMessage>(
    IMessageDescriptor descriptor,
    IMessageDependenciesFactory dependenciesFactory,
    IResultAdapterService? resultAdapterService,
    string[] groups,
    StagedVoidPlan<TMessage> plan) : IPipelineExecutor
    where TMessage : notnull, IMessage
{
    private readonly SingleAsyncHandlerMediationStrategy<TMessage> _strategy = new(resultAdapterService);

    private readonly ResultAdapterService? _concreteAdapters = resultAdapterService as ResultAdapterService;
    private readonly bool _foreignAdapters = resultAdapterService is not null and not ResultAdapterService;

    private IMessageDependencies? _cachedDependencies;
    private int _cachedVersion = int.MinValue;
    private bool _useStagedPlan;

    public ValueTask Execute(object message, IExecutionContext context, IServiceProvider serviceProvider)
    {
        var dependencies = GetDependencies();

        if (_useStagedPlan)
        {
            return plan.Execute((TMessage)message, context, serviceProvider);
        }

        return _strategy.Mediate((TMessage)message, dependencies, context, serviceProvider);
    }

    private IMessageDependencies GetDependencies()
    {
        if (dependenciesFactory is MessageDependenciesFactory typedFactory)
        {
            var cached = _cachedDependencies;

            if (cached is not null && _cachedVersion == typedFactory.CurrentRegistryVersion)
            {
                return cached;
            }

            var dependencies = typedFactory.Create(typeof(TMessage), descriptor, groups);
            _cachedDependencies = dependencies;
            _useStagedPlan = !_foreignAdapters
                && (_concreteAdapters is null || _concreteAdapters.IsEmpty)
                && dependencies is MessageDependencies { MemoizedInstances: false } fastDependencies
                && StagedPlanGate.Matches(fastDependencies, plan.Composition);
            _cachedVersion = typedFactory.CurrentRegistryVersion;
            return dependencies;
        }

        _useStagedPlan = false;
        return dependenciesFactory.Create(typeof(TMessage), descriptor, groups);
    }
}
