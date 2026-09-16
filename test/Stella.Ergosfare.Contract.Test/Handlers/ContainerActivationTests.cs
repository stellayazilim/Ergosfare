using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Handlers;

[Trait("Category", "Contract")]
public class ContainerActivationTests
{
    private static ServiceProvider Build(IServiceCollection services)
        => services.AddErgosfare(options => options.AddQueryModule(queries =>
                queries.AddGenerated("contract.activation")))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 42)]
    public async Task ContainerSelectsTheResolvableConstructor(bool withDependency, int expected)
    {
        var services = new ServiceCollection();
        if (withDependency) services.AddSingleton(new Dependency(42));
        await using var provider = Build(services);
        Assert.Equal(expected, await provider.GetRequiredService<IQueryMediator>().QueryAsync(new Multiple()));
        var registration = Assert.Single(services, d => d.ServiceType == typeof(MultipleHandler));
        Assert.Equal(typeof(MultipleHandler), registration.ImplementationType);
        Assert.Null(registration.ImplementationFactory);
    }

    [Fact]
    public async Task OptionalArrayAndRequiredMemberShapesUseContainerActivation()
    {
        await using var provider = Build(new ServiceCollection());
        var mediator = provider.GetRequiredService<IQueryMediator>();
        Assert.Equal(17, await mediator.QueryAsync(new Optional()));
        Assert.Equal(0, await mediator.QueryAsync(new ArrayParameter()));
        Assert.Equal(23, await mediator.QueryAsync(new RequiredMember()));
    }

    [Fact]
    public async Task RegisteredArrayIsInjected()
    {
        await using var provider = Build(new ServiceCollection().AddSingleton(new[] { new Dependency(1), new Dependency(2) }));
        Assert.Equal(2, await provider.GetRequiredService<IQueryMediator>().QueryAsync(new ArrayParameter()));
    }

    [Fact]
    public async Task ScopedFactoryOverrideAndDisposalRemainContainerOwned()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddScoped(_ => { calls++; return new MultipleHandler(new Dependency(91)); });
        await using var provider = Build(services);
        MultipleHandler first;
        await using (var scope = provider.CreateAsyncScope())
        {
            first = scope.ServiceProvider.GetRequiredService<MultipleHandler>();
            var mediator = scope.ServiceProvider.GetRequiredService<IQueryMediator>();
            Assert.Equal(91, await mediator.QueryAsync(new Multiple()));
            Assert.Equal(91, await mediator.QueryAsync(new Multiple()));
            Assert.Equal(1, calls);
            Assert.False(first.Disposed);
        }
        Assert.True(first.Disposed);
        await using var second = provider.CreateAsyncScope();
        Assert.NotSame(first, second.ServiceProvider.GetRequiredService<MultipleHandler>());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GeneratedTransientRegistrationTracksDisposalInDispatchScope()
    {
        var instances = new List<MultipleHandler>();
        await using var provider = Build(new ServiceCollection().AddSingleton(new Dependency(42, instances)));
        await using (var scope = provider.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IQueryMediator>();
            await mediator.QueryAsync(new Multiple());
            await mediator.QueryAsync(new Multiple());
            Assert.Equal(2, instances.Count);
            Assert.All(instances, handler => Assert.False(handler.Disposed));
        }
        Assert.All(instances, handler => Assert.True(handler.Disposed));
    }

    public sealed record Dependency(int Value, List<MultipleHandler>? Instances = null);

    [DiscoveryKey("contract.activation")]
    public sealed record Multiple : IQuery<int>;

    [DiscoveryKey("contract.activation")]
    public sealed class MultipleHandler : IQueryHandler<Multiple, int>, IDisposable
    {
        private readonly int value = 1;
        public bool Disposed { get; private set; }
        public MultipleHandler() { }
        public MultipleHandler(Dependency dependency)
        {
            value = dependency.Value;
            dependency.Instances?.Add(this);
        }
        public ValueTask<int> HandleAsync(Multiple query, ErgosfareContext context) => new(value);
        public void Dispose() => Disposed = true;
    }

    [DiscoveryKey("contract.activation")]
    public sealed record Optional : IQuery<int>;

    [DiscoveryKey("contract.activation")]
    public sealed class OptionalHandler(int value = 17) : IQueryHandler<Optional, int>
    {
        public ValueTask<int> HandleAsync(Optional query, ErgosfareContext context) => new(value);
    }

    [DiscoveryKey("contract.activation")]
    public sealed record ArrayParameter : IQuery<int>;

    [DiscoveryKey("contract.activation")]
    public sealed class ArrayHandler(Dependency[]? values = null) : IQueryHandler<ArrayParameter, int>
    {
        public ValueTask<int> HandleAsync(ArrayParameter query, ErgosfareContext context) => new(values?.Length ?? 0);
    }

    [DiscoveryKey("contract.activation")]
    public sealed record RequiredMember : IQuery<int>;

    [DiscoveryKey("contract.activation")]
    public sealed class RequiredHandler : IQueryHandler<RequiredMember, int>
    {
        public required string Label { get; init; }
        public ValueTask<int> HandleAsync(RequiredMember query, ErgosfareContext context) => new(23);
    }
}
