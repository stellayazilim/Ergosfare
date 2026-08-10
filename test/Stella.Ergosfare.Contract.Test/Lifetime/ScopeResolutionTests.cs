using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Lifetime;

/// <summary>
/// Which DI scope a handler's dependencies come from: the scope that resolved the
/// mediator, not the container root.
/// </summary>
public sealed class ScopeResolutionTests
{
    private const string Key = "contract.scope";

    /// <summary>Registered scoped, so its identity names the scope it came from.</summary>
    public sealed class ScopedDependency
    {
        /// <summary>Unique per constructed instance.</summary>
        public Guid Id { get; } = Guid.NewGuid();
    }

    [DiscoveryKey(Key)]
    public sealed class NeedsScope : ICommand
    {
        /// <summary>The dependency identity the handler was given.</summary>
        public Guid SeenId;
    }

    [DiscoveryKey(Key)]
    public sealed class NeedsScopeHandler(ScopedDependency dependency) : ICommandHandler<NeedsScope>
    {
        public ValueTask HandleAsync(NeedsScope command, IExecutionContext context)
        {
            command.SeenId = dependency.Id;
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddScoped<ScopedDependency>()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

    private static async Task<Guid> DispatchIn(IServiceScope scope)
    {
        var command = new NeedsScope();
        await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(command);
        return command.SeenId;
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Two_dispatches_in_one_scope_see_the_same_scoped_dependency()
    {
        await using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var first = await DispatchIn(scope);
        var second = await DispatchIn(scope);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Dispatches_in_different_scopes_see_different_scoped_dependencies()
    {
        await using var provider = CreateProvider();

        Guid first;
        Guid second;

        using (var scope = provider.CreateScope())
        {
            first = await DispatchIn(scope);
        }

        using (var scope = provider.CreateScope())
        {
            second = await DispatchIn(scope);
        }

        Assert.NotEqual(first, second);
    }
}
