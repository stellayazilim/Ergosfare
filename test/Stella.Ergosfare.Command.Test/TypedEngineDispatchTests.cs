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
    /// A generic message: an open definition is not a dispatchable message, and the
    /// generator does not plan generic message types at all — no closed form of one is
    /// ever rooted, so every dispatch of one fails naming the missing plan.
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
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedDispatch_DoesNotServeAnotherContainersExecutor()
    {
        // The message registry is process-wide, so a second container can dispatch the
        // same message type. What must not leak between containers is the executor: it
        // carries the container's dependencies factory — and with it the container's
        // dispatch verdict, made observable here via ForceMemoizedHandlers, whose
        // pipelines a compiled plan refuses to serve.
        await using var transientContainer = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<IdentityProbeCommandHandler>()))
            .BuildServiceProvider();
        var transientEngine = transientContainer.GetRequiredService<MessageDispatchEngine>();

        // Populate the static-generic slot from the transient container: every dispatch
        // resolves a fresh handler instance.
        var first = await DispatchAndReadId(transientEngine, transientContainer);
        var second = await DispatchAndReadId(transientEngine, transientContainer);
        Assert.NotEqual(first, second);

        await using var memoizedContainer = new ServiceCollection()
            .AddErgosfare(x =>
            {
                x.ForceMemoizedHandlers();
                x.AddCommandModule(c => c.Register<IdentityProbeCommandHandler>());
            })
            .BuildServiceProvider();
        var memoizedEngine = memoizedContainer.GetRequiredService<MessageDispatchEngine>();

        // Served through its own executor the memoized container fails loudly — its
        // pipeline memoizes instances, which no compiled plan serves. Being served the
        // transient container's cached executor instead would dispatch just fine, which
        // is exactly the leak the holder's cache-identity guard exists to prevent.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await memoizedEngine.DispatchVoidAsync(new IdentityProbeCommand(), new ErgosfareContext(), memoizedContainer));
        Assert.Equal(UnplannedDispatchReason.MemoizedInstances, thrown.Reason);

        // And after the slot moved on, the original container still dispatches through
        // its own transient-resolving executor.
        var third = await DispatchAndReadId(transientEngine, transientContainer);
        Assert.NotEqual(first, third);

        static async Task<Guid> DispatchAndReadId(MessageDispatchEngine engine, IServiceProvider provider)
        {
            var items = new ErgosfareContext();
            await engine.DispatchVoidAsync(new IdentityProbeCommand(), items, provider);
            return Assert.IsType<Guid>(items.Items["handlerId"]);
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
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedResultDispatch_DoesNotServeAnotherContainersExecutor()
    {
        // The result holder is keyed by the (message, result) pair and is process-wide,
        // while an executor belongs to one container. Without the cache-identity guard the
        // second container would run the first one's pipeline — observable here because a
        // memoized container's own executor refuses to dispatch, exactly as the void
        // lane's twin asserts it.
        await using var transientContainer = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ResultIdentityProbeCommandHandler>()))
            .BuildServiceProvider();
        var transientEngine = transientContainer.GetRequiredService<MessageDispatchEngine>();

        var first = await DispatchAndReadId(transientEngine, transientContainer);
        var second = await DispatchAndReadId(transientEngine, transientContainer);
        Assert.NotEqual(first, second);

        await using var memoizedContainer = new ServiceCollection()
            .AddErgosfare(x =>
            {
                x.ForceMemoizedHandlers();
                x.AddCommandModule(c => c.Register<ResultIdentityProbeCommandHandler>());
            })
            .BuildServiceProvider();
        var memoizedEngine = memoizedContainer.GetRequiredService<MessageDispatchEngine>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await memoizedEngine.DispatchAsync<ResultIdentityProbeCommand, Guid>(
                new ResultIdentityProbeCommand(), new ErgosfareContext(), memoizedContainer));
        Assert.Equal(UnplannedDispatchReason.MemoizedInstances, thrown.Reason);

        // And after the slot moved on, the original container still resolves transiently.
        var third = await DispatchAndReadId(transientEngine, transientContainer);
        Assert.NotEqual(first, third);

        static async Task<Guid> DispatchAndReadId(MessageDispatchEngine engine, IServiceProvider provider)
        {
            var items = new ErgosfareContext();

            return await engine.DispatchAsync<ResultIdentityProbeCommand, Guid>(
                new ResultIdentityProbeCommand(), items, provider);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AGenericMessage_IsUnplanned_AndFailsBothLanes()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // The generator does not plan generic message types, and nothing is dispatched at
        // run time that was not produced at compile time — so the closed form fails on
        // both the typed and the erased lane, naming the missing plan, until the
        // generator learns the construct.
        var typed = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await engine.DispatchAsync<WrappedProbe<int>, string>(
                new WrappedProbe<int>(), new ErgosfareContext(), provider));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, typed.Reason);

        await using var fresh = BuildProvider();
        var erased = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await fresh.GetRequiredService<MessageDispatchEngine>()
                .DispatchAsync<string>(new WrappedProbe<int>(), fresh));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, erased.Reason);
    }
}
