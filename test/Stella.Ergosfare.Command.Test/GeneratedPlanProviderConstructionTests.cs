using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Runtime behavior of the plans' provider-taking construction factories — the shape the
/// generator emits for handlers with constructor dependencies. The same gate as the
/// parameterless factories applies (plain transient registration only, lifetime overrides
/// route back through the container); on top of that, the factory must resolve every
/// dependency from the dispatching scope's provider, exactly where container activation
/// would resolve it. Test factories construct marked instances, which generated code never
/// does, precisely so the chosen construction path is observable. Helper types are
/// excluded from discovery so assembly scans (the registry is process-wide) cannot alter
/// these pipelines.
/// </summary>
public class GeneratedPlanProviderConstructionTests
{
    public interface IProbeDependency
    {
        Guid Id { get; }
    }

    public sealed class ProbeDependency : IProbeDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    [ExcludeFromDiscovery]
    public sealed class InjectedCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class InjectedCommandHandler(IProbeDependency dependency) : ICommandHandler<InjectedCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public bool ViaPlanFactory { get; init; }

        public ValueTask HandleAsync(InjectedCommand command, IExecutionContext context)
        {
            context.Set("handlerId", _id);
            context.Set("dependencyId", dependency.Id);
            context.Set("viaPlanFactory", ViaPlanFactory);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PlainTransientRegistration_ConstructsThroughTheProviderFactory()
    {
        GeneratedDispatchRoots.AddVoidPlan<InjectedCommand, InjectedCommandHandler>(
            static provider => new InjectedCommandHandler(provider.GetRequiredService<IProbeDependency>())
            {
                ViaPlanFactory = true,
            });

        var provider = new ServiceCollection()
            .AddSingleton<IProbeDependency, ProbeDependency>()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<InjectedCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var first = new CommandMediationSettings();
        await mediator.SendAsync(new InjectedCommand(), first);
        var second = new CommandMediationSettings();
        await mediator.SendAsync(new InjectedCommand(), second);

        // The factory constructs (transient semantics intact — fresh handler per
        // dispatch), and the dependency is the container's own singleton instance.
        Assert.Equal(true, first.Items["viaPlanFactory"]);
        Assert.Equal(true, second.Items["viaPlanFactory"]);
        Assert.NotEqual(first.Items["handlerId"], second.Items["handlerId"]);
        Assert.Equal(provider.GetRequiredService<IProbeDependency>().Id, first.Items["dependencyId"]);
        Assert.Equal(provider.GetRequiredService<IProbeDependency>().Id, second.Items["dependencyId"]);
    }

    [ExcludeFromDiscovery]
    public sealed class ScopedInjectedCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class ScopedInjectedCommandHandler(IProbeDependency dependency) : ICommandHandler<ScopedInjectedCommand>
    {
        public ValueTask HandleAsync(ScopedInjectedCommand command, IExecutionContext context)
        {
            context.Set("dependencyId", dependency.Id);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ScopedDependency_ResolvesFromTheDispatchingScope()
    {
        GeneratedDispatchRoots.AddVoidPlan<ScopedInjectedCommand, ScopedInjectedCommandHandler>(
            static provider => new ScopedInjectedCommandHandler(provider.GetRequiredService<IProbeDependency>()));

        var provider = new ServiceCollection()
            .AddScoped<IProbeDependency, ProbeDependency>()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ScopedInjectedCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        // Container activation resolves a scoped dependency from the resolving scope;
        // the provider factory must land on the very same instance.
        using var firstScope = provider.CreateScope();
        var firstSettings = new CommandMediationSettings();
        await firstScope.ServiceProvider.GetRequiredService<ICommandMediator>()
            .SendAsync(new ScopedInjectedCommand(), firstSettings);

        Assert.Equal(
            firstScope.ServiceProvider.GetRequiredService<IProbeDependency>().Id,
            firstSettings.Items["dependencyId"]);

        using var secondScope = provider.CreateScope();
        var secondSettings = new CommandMediationSettings();
        await secondScope.ServiceProvider.GetRequiredService<ICommandMediator>()
            .SendAsync(new ScopedInjectedCommand(), secondSettings);

        Assert.Equal(
            secondScope.ServiceProvider.GetRequiredService<IProbeDependency>().Id,
            secondSettings.Items["dependencyId"]);
        Assert.NotEqual(firstSettings.Items["dependencyId"], secondSettings.Items["dependencyId"]);
    }

    [ExcludeFromDiscovery]
    public sealed class OverriddenInjectedCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class OverriddenInjectedCommandHandler(IProbeDependency dependency) : ICommandHandler<OverriddenInjectedCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public ValueTask HandleAsync(OverriddenInjectedCommand command, IExecutionContext context)
        {
            _ = dependency;
            context.Set("handlerId", _id);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task SingletonLifetimeOverride_KeepsTheSharedInstance()
    {
        GeneratedDispatchRoots.AddVoidPlan<OverriddenInjectedCommand, OverriddenInjectedCommandHandler>(
            static provider => new OverriddenInjectedCommandHandler(provider.GetRequiredService<IProbeDependency>()));

        var provider = new ServiceCollection()
            .AddSingleton<IProbeDependency, ProbeDependency>()
            .AddSingleton<OverriddenInjectedCommandHandler>()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<OverriddenInjectedCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // A provider-factory construction would hand out fresh instances; the user's
        // singleton override must keep sharing one — the gate routes back through the
        // container exactly as it does for parameterless factories.
        var first = new CommandMediationSettings();
        await mediator.SendAsync(new OverriddenInjectedCommand(), first);
        var second = new CommandMediationSettings();
        await mediator.SendAsync(new OverriddenInjectedCommand(), second);

        Assert.Equal(first.Items["handlerId"], second.Items["handlerId"]);
    }
}
