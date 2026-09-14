using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class TypedProbeCommand : ICommand { }

public sealed class TypedProbeCommandHandler : ICommandHandler<TypedProbeCommand>
{
    public ValueTask HandleAsync(TypedProbeCommand command, ErgosfareContext context)
    {
        context.Set("typedProbe", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class TypedUnregisteredCommand : ICommand { }

public sealed class IdentityProbeCommand : ICommand { }

public sealed class IdentityProbeCommandHandler : ICommandHandler<IdentityProbeCommand>
{
    private readonly Guid _id = Guid.NewGuid();

    public ValueTask HandleAsync(IdentityProbeCommand command, ErgosfareContext context)
    {
        context.Set("handlerId", _id);
        return ValueTask.CompletedTask;
    }
}

public sealed class TypedResultProbeCommand : ICommand<string> { }

public sealed class TypedResultProbeCommandHandler : ICommandHandler<TypedResultProbeCommand, string>
{
    public ValueTask<string> HandleAsync(TypedResultProbeCommand command, ErgosfareContext context)
        => ValueTask.FromResult("typed");
}

public sealed class ResultIdentityProbeCommand : ICommand<Guid> { }

public sealed class ResultIdentityProbeCommandHandler : ICommandHandler<ResultIdentityProbeCommand, Guid>
{
    private readonly Guid _id = Guid.NewGuid();

    public ValueTask<Guid> HandleAsync(ResultIdentityProbeCommand command, ErgosfareContext context)
        => ValueTask.FromResult(_id);
}

/// <summary>
/// Covers the typed void dispatch overload on <see cref="MessageDispatchEngine"/>: the
/// static-generic executor holder serves the exact-typed call, base-typed generic calls
/// keep resolving by the message's runtime type, and the holder's cache-identity guard
/// keeps containers isolated from each other's cached executors.
/// </summary>
public class TypedEngineDispatchTests
{
    /// <summary>
    /// A generic message whose int instantiation is supplied by the selected handler contract.
    /// </summary>
    public sealed class WrappedProbe<T> : ICommand<string> { }

    public sealed class WrappedProbeHandler : ICommandHandler<WrappedProbe<int>, string>
    {
        public ValueTask<string> HandleAsync(WrappedProbe<int> command, ErgosfareContext context)
            => ValueTask.FromResult("wrapped");
    }

    private static ServiceProvider BuildProvider()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<TypedProbeCommandHandler>();
                c.Register<TypedResultProbeCommandHandler>();
                c.Register<WrappedProbeHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedDispatch_RunsThePipeline_AndRepeatsThroughTheHolder()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // First call populates the static-generic slot, second call is served from it;
        // both must run the full pipeline.
        for (var i = 0; i < 2; i++)
        {
            var items = new ErgosfareContext();

            await engine.DispatchVoidAsync(new TypedProbeCommand(), items, provider);

            Assert.Equal(true, items.Items["typedProbe"]);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedDispatch_BaseTypedGenericCall_ResolvesByRuntimeType()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // TMessage closes over ICommand here, so the holder guard must reject the slot
        // and resolve the executor by the runtime type — the concrete pipeline still runs.
        ICommand baseTyped = new TypedProbeCommand();
        var items = new ErgosfareContext();

        await engine.DispatchVoidAsync(baseTyped, items, provider);

        Assert.Equal(true, items.Items["typedProbe"]);
    }

    [Fact]
    public async Task TypedDispatch_DoesNotServeAnotherContainersExecutor()
    {
        // Generated plans are shared, but each container selects its own registrations.
        await using var registered = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<IdentityProbeCommandHandler>()))
            .BuildServiceProvider();
        var engine = registered.GetRequiredService<MessageDispatchEngine>();
        var first = await DispatchAndReadId(engine, registered);
        var second = await DispatchAndReadId(engine, registered);
        Assert.NotEqual(first, second);

        await using var empty = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => { }))
            .BuildServiceProvider();
        var rejected = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await DispatchAndReadId(empty.GetRequiredService<MessageDispatchEngine>(), empty));
        Assert.Equal(UnplannedDispatchReason.CompositionDiverged, rejected.Reason);

        var third = await DispatchAndReadId(engine, registered);
        Assert.NotEqual(first, third);

        static async Task<Guid> DispatchAndReadId(MessageDispatchEngine engine, IServiceProvider provider)
        {
            var context = new ErgosfareContext();
            await engine.DispatchVoidAsync(new IdentityProbeCommand(), context, provider);
            return Assert.IsType<Guid>(context.Items["handlerId"]);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedDispatch_UnregisteredMessage_FailsLikeErasedDispatch()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // The registry is process-wide, so suites running in parallel may add assignable
        // descriptors (say, an interceptor-only base-interface pipeline) that change WHICH
        // exception an unhandled message produces. The contract under test is exception
        // parity: the typed overload fails exactly like the erased one.
        var erased = await Record.ExceptionAsync(async () =>
            await engine.DispatchAsync(new TypedUnregisteredCommand(), provider));
        var typed = await Record.ExceptionAsync(async () =>
            await engine.DispatchVoidAsync(new TypedUnregisteredCommand(), provider));

        Assert.NotNull(erased);
        Assert.NotNull(typed);
        Assert.IsType(erased.GetType(), typed);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedResultDispatch_RunsThePipeline_AndRepeatsThroughTheHolder()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // First call populates the (message, result) slot, second is served from it.
        for (var i = 0; i < 2; i++)
        {
            var items = new ErgosfareContext();

            var result = await engine.DispatchAsync<TypedResultProbeCommand, string>(
                new TypedResultProbeCommand(), items, provider);

            Assert.Equal("typed", result);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedResultDispatch_BaseTypedGenericCall_ResolvesByRuntimeType()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // TMessage closes over ICommand<string>, so the runtime-type guard must reject the
        // slot and resolve by the concrete type — naming a base type stays legal.
        ICommand<string> baseTyped = new TypedResultProbeCommand();
        var items = new ErgosfareContext();

        var result = await engine.DispatchAsync<ICommand<string>, string>(baseTyped, items, provider);

        Assert.Equal("typed", result);
    }

    [Fact]
    public async Task TypedResultDispatch_DoesNotServeAnotherContainersExecutor()
    {
        // Generated plans are shared, but each container selects its own registrations.
        await using var registered = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ResultIdentityProbeCommandHandler>()))
            .BuildServiceProvider();
        var engine = registered.GetRequiredService<MessageDispatchEngine>();
        var first = await DispatchAndReadId(engine, registered);
        var second = await DispatchAndReadId(engine, registered);
        Assert.NotEqual(first, second);

        await using var empty = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => { }))
            .BuildServiceProvider();
        var rejected = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await DispatchAndReadId(empty.GetRequiredService<MessageDispatchEngine>(), empty));
        Assert.Equal(UnplannedDispatchReason.CompositionDiverged, rejected.Reason);

        var third = await DispatchAndReadId(engine, registered);
        Assert.NotEqual(first, third);

        static async Task<Guid> DispatchAndReadId(MessageDispatchEngine engine, IServiceProvider provider)
        {
            var context = new ErgosfareContext();
            return await engine.DispatchAsync<ResultIdentityProbeCommand, Guid>(new ResultIdentityProbeCommand(), context, provider);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AClosedGenericMessage_ExecutesThroughBothLanes()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        var typed = await engine.DispatchAsync<WrappedProbe<int>, string>(
            new WrappedProbe<int>(), new ErgosfareContext(), provider);
        Assert.Equal("wrapped", typed);

        await using var fresh = BuildProvider();
        var erased = await fresh.GetRequiredService<MessageDispatchEngine>()
            .DispatchAsync<string>(new WrappedProbe<int>(), fresh);
        Assert.Equal("wrapped", erased);
    }
}
