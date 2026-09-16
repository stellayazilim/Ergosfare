using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

[ExcludeFromPipeline]
public sealed record FactoryProbe : ICommand<FactoryProbeHandler>;

public sealed class FactoryDependency;

[DiscoveryKey("startup.factories")]
public sealed class FactoryProbeHandler(FactoryDependency dependency) : ICommandHandler<FactoryProbe, FactoryProbeHandler>, IDisposable
{
    public FactoryDependency Dependency { get; } = dependency;
    public bool Disposed { get; private set; }
    public ValueTask<FactoryProbeHandler> HandleAsync(FactoryProbe command, ErgosfareContext context) => new(this);
    public void Dispose() => Disposed = true;
}

[ExcludeFromPipeline]
public sealed record CustomFactoryProbe : ICommand<string>;

[DiscoveryKey("startup.custom")]
public sealed class CustomFactoryHandler : ICommandHandler<CustomFactoryProbe, string>
{
    private readonly string _value;
    public CustomFactoryHandler() => _value = "default";
    public CustomFactoryHandler(string value) => _value = value;
    public ValueTask<string> HandleAsync(CustomFactoryProbe command, ErgosfareContext context) => new(_value);
}

[Trait("Category", "Unit")]
public class GeneratedStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedRegistration_PreservesScopeDisposalAndExplicitLifetime(bool scopedOverride)
    {
        var services = new ServiceCollection();
        services.AddScoped(static _ => new FactoryDependency());
        if (scopedOverride)
            services.AddScoped(static provider => new FactoryProbeHandler(provider.GetRequiredService<FactoryDependency>()));
        services.AddErgosfare(options => options.AddCommandModule(commands => commands.AddGenerated("startup.factories")));
        var descriptor = Assert.Single(services, s => s.ServiceType == typeof(FactoryProbeHandler));
        if (scopedOverride)
        {
            Assert.NotNull(descriptor.ImplementationFactory);
            Assert.Null(descriptor.ImplementationType);
        }
        else
        {
            Assert.Null(descriptor.ImplementationFactory);
            Assert.Equal(typeof(FactoryProbeHandler), descriptor.ImplementationType);
        }
        Assert.Equal(scopedOverride ? ServiceLifetime.Scoped : ServiceLifetime.Transient, descriptor.Lifetime);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        FactoryProbeHandler first;
        FactoryProbeHandler second;
        await using (var scope = provider.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();
            first = await mediator.SendAsync(new FactoryProbe());
            second = await mediator.SendAsync(new FactoryProbe());
            Assert.Equal(scopedOverride, ReferenceEquals(first, second));
            Assert.Same(first.Dependency, second.Dependency);
            Assert.False(first.Disposed);
        }
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
        await using var other = provider.CreateAsyncScope();
        var third = await other.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new FactoryProbe());
        Assert.NotSame(first.Dependency, third.Dependency);
    }

    [Theory]
    [InlineData(false, "default")]
    [InlineData(true, "injected")]
    public async Task MultipleConstructors_DelegateSelectionToContainer(bool registerDependency, string expected)
    {
        var services = new ServiceCollection();
        if (registerDependency) services.AddSingleton("injected");
        await using var provider = services
            .AddErgosfare(options => options.AddCommandModule(commands => commands.Register<CustomFactoryHandler>()))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CustomFactoryProbe()));
    }

    [Fact]
    public async Task ExplicitFactory_OverridesContainerConstructorSelection()
    {
        await using var provider = new ServiceCollection()
            .AddScoped(static _ => new CustomFactoryHandler("custom"))
            .AddErgosfare(options => options.AddCommandModule(commands => commands.Register<CustomFactoryHandler>()))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal("custom", await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(new CustomFactoryProbe()));
    }
}
