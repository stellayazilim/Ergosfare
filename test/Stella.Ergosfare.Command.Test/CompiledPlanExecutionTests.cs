using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class CompiledDirectCommand : ICommand { public int Calls; }
public sealed class CompiledDirectHandler : ICommandHandler<CompiledDirectCommand>
{
    public ValueTask HandleAsync(CompiledDirectCommand message, ErgosfareContext context)
    { message.Calls++; return default; }
}
public sealed record CompiledInjectedCommand : ICommand<Guid>;
public sealed class CompiledDependency { public Guid Id { get; } = Guid.NewGuid(); }
public sealed class CompiledInjectedHandler(CompiledDependency dependency) : ICommandHandler<CompiledInjectedCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CompiledInjectedCommand message, ErgosfareContext context)
        => ValueTask.FromResult(dependency.Id);
}

public class CompiledPlanExecutionTests
{
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddScoped<CompiledDependency>();
        services.AddErgosfare(r => r.AddCommandModule(m =>
        {
            m.Register<CompiledDirectHandler>();
            m.Register<CompiledInjectedHandler>();
        }));
        return services;
    }

    [Fact]
    public async Task ParameterlessHandler_IsConstructedByTheGeneratedBody()
    {
        var services = Services();
        services.AddTransient<CompiledDirectHandler>(_ => throw new Exception("DI must not construct this handler"));
        await using var provider = services.BuildServiceProvider();
        var command = new CompiledDirectCommand();
        await provider.GetRequiredService<MessageDispatchEngine>().DispatchAsync(command, provider);
        Assert.Equal(1, command.Calls);
    }

    [Fact]
    public async Task InjectedHandler_UsesTheCallingScope()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        var a = await engine.DispatchAsync<Guid>(new CompiledInjectedCommand(), first.ServiceProvider);
        var b = await engine.DispatchAsync<Guid>(new CompiledInjectedCommand(), second.ServiceProvider);
        Assert.Equal(first.ServiceProvider.GetRequiredService<CompiledDependency>().Id, a);
        Assert.Equal(second.ServiceProvider.GetRequiredService<CompiledDependency>().Id, b);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task InjectedHandler_HonorsAServiceFactoryOverride()
    {
        var services = Services();
        var dependency = new CompiledDependency();
        var calls = 0;
        services.AddTransient<CompiledInjectedHandler>(_ => { calls++; return new(dependency); });
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        Assert.Equal(dependency.Id, await engine.DispatchAsync<Guid>(new CompiledInjectedCommand(), provider));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task InjectedHandler_HonorsSingletonLifetimeAcrossScopes()
    {
        var services = Services();
        var dependency = new CompiledDependency();
        services.AddSingleton(new CompiledInjectedHandler(dependency));
        await using var provider = services.BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        Assert.Equal(dependency.Id, await engine.DispatchAsync<Guid>(new CompiledInjectedCommand(), first.ServiceProvider));
        Assert.Equal(dependency.Id, await engine.DispatchAsync<Guid>(new CompiledInjectedCommand(), second.ServiceProvider));
    }

    [Fact]
    public void TheGeneratedPlan_IsTheExecutorAndCarriesItsDescriptor()
    {
        var plan = GeneratedPlanRegistry.FindStagedVoidPlan(typeof(CompiledDirectCommand));
        Assert.NotNull(plan);
        Assert.IsAssignableFrom<IPipelineExecutor>(plan);
        Assert.Equal(typeof(CompiledDirectHandler), plan.Composition.HandlerType);
        Assert.Same(plan, GeneratedPlanRegistry.FindStagedVoidPlan(typeof(CompiledDirectCommand)));
    }

    private sealed class RejectServiceLookups : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => throw new InvalidOperationException($"Unexpected dispatch-time DI lookup: {serviceType}");
    }

    [Fact]
    public async Task DirectParticipantDispatch_DoesNotResolveOptionsOrAdapters()
    {
        using var provider = Services().BuildServiceProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        var command = new CompiledDirectCommand();
        var context = new ErgosfareContext();
        await engine.DispatchAsync(command, context, new RejectServiceLookups());
        await engine.DispatchAsync(command, new RejectServiceLookups());
        Assert.Equal(2, command.Calls);
    }

    // These bodies complete synchronously. Keeping the measurement on one thread is intentional.
    #pragma warning disable xUnit1031
    [Fact]
    public void Dispatch_DoesNotAllocateAnInfrastructureGraph()
    {
        using var provider = Services().BuildServiceProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        var command = new CompiledDirectCommand();
        var context = new ErgosfareContext();
        for (var i = 0; i < 20; i++) engine.DispatchAsync(command, context, provider).GetAwaiter().GetResult();
        var plan = (IPipelineExecutor)GeneratedPlanRegistry.FindStagedVoidPlan(typeof(CompiledDirectCommand))!;
        var baselineStart = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) plan.Execute(command, context, provider, null).GetAwaiter().GetResult();
        var bodyAllocations = GC.GetAllocatedBytesForCurrentThread() - baselineStart;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) engine.DispatchAsync(command, context, provider).GetAwaiter().GetResult();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // A 24-byte parameterless handler per call is the only required object. The JIT
        // may eliminate even that allocation; there must be no executor/shape/reference graph.
        Assert.InRange(allocated, 0, bodyAllocations);
        Assert.Equal(220, command.Calls);
    }
}
