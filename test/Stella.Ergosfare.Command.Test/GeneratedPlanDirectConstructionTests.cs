using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Runtime gating of the plans' direct-construction factories: the factory is used only
/// while the handler's effective DI registration is the module's own plain transient one,
/// and any user customization — a factory registration, a lifetime override, forced
/// memoization — routes the dispatch back through the container. The tests install plans
/// whose factories construct marked instances, which generated code never does, precisely
/// so the chosen construction path is observable. Helper types are excluded from discovery
/// so assembly scans (the registry is process-wide) cannot alter these pipelines.
/// </summary>
public class GeneratedPlanDirectConstructionTests
{
    [ExcludeFromDiscovery]
    public sealed class ConstructedCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class ConstructedCommandHandler : ICommandHandler<ConstructedCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public bool ViaPlanFactory { get; init; }

        public ValueTask HandleAsync(ConstructedCommand command, ErgosfareContext context)
        {
            context.Set("handlerId", _id);
            context.Set("viaPlanFactory", ViaPlanFactory);
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<(Guid Id, bool ViaPlanFactory)> DispatchAndProbe(ICommandMediator mediator)
    {
        var settings = new Dictionary<object, object?>();
        await mediator.SendAsync(new ConstructedCommand(), settings);
        return (Assert.IsType<Guid>(settings["handlerId"]),
            Assert.IsType<bool>(settings["viaPlanFactory"]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PlainTransientRegistration_ConstructsThroughThePlanFactory()
    {
        GeneratedDispatchRoots.AddVoidPlan<ConstructedCommand, ConstructedCommandHandler>(
            static () => new ConstructedCommandHandler { ViaPlanFactory = true });

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ConstructedCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The module's own plain transient registration: the factory constructs, and
        // transient semantics stay — a fresh instance per dispatch.
        var first = await DispatchAndProbe(mediator);
        var second = await DispatchAndProbe(mediator);

        Assert.True(first.ViaPlanFactory);
        Assert.True(second.ViaPlanFactory);
        Assert.NotEqual(first.Id, second.Id);
    }

    [ExcludeFromDiscovery]
    public sealed class UserFactoryCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class UserFactoryCommandHandler : ICommandHandler<UserFactoryCommand>
    {
        public bool ViaUserFactory { get; init; }

        public ValueTask HandleAsync(UserFactoryCommand command, ErgosfareContext context)
        {
            context.Set("viaUserFactory", ViaUserFactory);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UserFactoryRegistration_KeepsResolvingThroughTheContainer()
    {
        GeneratedDispatchRoots.AddVoidPlan<UserFactoryCommand, UserFactoryCommandHandler>(
            static () => new UserFactoryCommandHandler());

        // The user's factory registration (made before AddErgosfare, so the module's
        // TryAddTransient defers to it) customizes construction — the plan factory would
        // skip that customization and must therefore stay unused.
        var provider = new ServiceCollection()
            .AddTransient(_ => new UserFactoryCommandHandler { ViaUserFactory = true })
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<UserFactoryCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var settings = new Dictionary<object, object?>();
        await mediator.SendAsync(new UserFactoryCommand(), settings);

        Assert.Equal(true, settings["viaUserFactory"]);
    }

    [ExcludeFromDiscovery]
    public sealed class LateOverrideCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class LateOverrideCommandHandler : ICommandHandler<LateOverrideCommand>
    {
        public bool ViaUser { get; init; }

        public ValueTask HandleAsync(LateOverrideCommand command, ErgosfareContext context)
        {
            context.Set("viaUser", ViaUser);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task RegistrationAfterAddErgosfare_KeepsResolvingThroughTheContainer()
    {
        GeneratedDispatchRoots.AddVoidPlan<LateOverrideCommand, LateOverrideCommandHandler>(
            static () => new LateOverrideCommandHandler());

        // The override lands AFTER AddErgosfare but before BuildServiceProvider — the
        // window a snapshot taken inside AddErgosfare would miss. The lifetime capture
        // runs at first resolution, so the user's instance must win over the plan factory
        // exactly as it wins over GetRequiredService.
        var services = new ServiceCollection();
        services.AddErgosfare(x => x.AddCommandModule(c => c.Register<LateOverrideCommandHandler>()));
        services.AddSingleton(new LateOverrideCommandHandler { ViaUser = true });

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var settings = new Dictionary<object, object?>();
        await mediator.SendAsync(new LateOverrideCommand(), settings);

        Assert.Equal(true, settings["viaUser"]);
    }

    [ExcludeFromDiscovery]
    public sealed class SingletonCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class SingletonCommandHandler : ICommandHandler<SingletonCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public ValueTask HandleAsync(SingletonCommand command, ErgosfareContext context)
        {
            context.Set("handlerId", _id);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task SingletonLifetimeOverride_KeepsTheSharedInstance()
    {
        GeneratedDispatchRoots.AddVoidPlan<SingletonCommand, SingletonCommandHandler>(
            static () => new SingletonCommandHandler());

        var provider = new ServiceCollection()
            .AddSingleton<SingletonCommandHandler>()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<SingletonCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // A plan-factory construction would hand out fresh instances; the user's
        // singleton override must keep sharing one.
        var first = new Dictionary<object, object?>();
        await mediator.SendAsync(new SingletonCommand(), first);
        var second = new Dictionary<object, object?>();
        await mediator.SendAsync(new SingletonCommand(), second);

        Assert.Equal(first["handlerId"], second["handlerId"]);
    }

    [ExcludeFromDiscovery]
    public sealed class MemoizedPlanCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class MemoizedPlanCommandHandler : ICommandHandler<MemoizedPlanCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public ValueTask HandleAsync(MemoizedPlanCommand command, ErgosfareContext context)
        {
            context.Set("handlerId", _id);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ForceMemoizedHandlers_KeepsTheMemoizedInstance()
    {
        GeneratedDispatchRoots.AddVoidPlan<MemoizedPlanCommand, MemoizedPlanCommandHandler>(
            static () => new MemoizedPlanCommandHandler());

        var provider = new ServiceCollection()
            .AddErgosfare(x =>
            {
                x.ForceMemoizedHandlers();
                x.AddCommandModule(c => c.Register<MemoizedPlanCommandHandler>());
            })
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var first = new Dictionary<object, object?>();
        await mediator.SendAsync(new MemoizedPlanCommand(), first);
        var second = new Dictionary<object, object?>();
        await mediator.SendAsync(new MemoizedPlanCommand(), second);

        Assert.Equal(first["handlerId"], second["handlerId"]);
    }
}
