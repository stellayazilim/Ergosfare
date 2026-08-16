using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Covers the typed void dispatch overload on <see cref="MessageDispatchEngine"/>: the
/// static-generic executor holder serves the exact-typed call, base-typed generic calls
/// keep resolving by the message's runtime type, and the holder's cache-identity guard
/// keeps containers isolated from each other's cached executors.
/// </summary>
public class TypedEngineDispatchTests
{
    public sealed class TypedProbeCommand : ICommand { }

    // ERGO007 (suppressed in the csproj): delivered through the engine's typed dispatch
    // overloads below, which the closed-world dispatch-site analysis cannot see.
    public sealed class TypedProbeCommandHandler : ICommandHandler<TypedProbeCommand>
    {
        public ValueTask HandleAsync(TypedProbeCommand command, ErgosfareContext context)
        {
            context.Set("typedProbe", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class UnregisteredCommand : ICommand { }

    public sealed class IdentityProbeCommand : ICommand { }

    // ERGO007 (suppressed in the csproj): delivered through the engine's typed dispatch
    // overloads below.
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

    // ERGO007 (suppressed in the csproj): delivered through the engine's typed dispatch
    // overloads below.
    public sealed class TypedResultProbeCommandHandler : ICommandHandler<TypedResultProbeCommand, string>
    {
        public ValueTask<string> HandleAsync(TypedResultProbeCommand command, ErgosfareContext context)
            => ValueTask.FromResult("typed");
    }

    public sealed class ResultIdentityProbeCommand : ICommand<Guid> { }

    // ERGO007 (suppressed in the csproj): delivered through the engine's typed dispatch
    // overloads below.
    public sealed class ResultIdentityProbeCommandHandler : ICommandHandler<ResultIdentityProbeCommand, Guid>
    {
        private readonly Guid _id = Guid.NewGuid();

        public ValueTask<Guid> HandleAsync(ResultIdentityProbeCommand command, ErgosfareContext context)
            => ValueTask.FromResult(_id);
    }

    /// <summary>
    /// A generic message: an open definition is not a dispatchable message, so no closed
    /// form of it is ever rooted. The untyped lane therefore has to close
    /// <c>FrozenResultDispatch&lt;,&gt;</c> reflectively for it; the typed lane never asks.
    /// </summary>
    public sealed class WrappedProbe<T> : ICommand<string> { }

    // ERGO007 (suppressed in the csproj): delivered through the engine's typed dispatch
    // overloads below.
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
        // lifetime semantics, made observable here via ForceMemoizedHandlers.
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

        // Served through its own executor the memoized container reuses one handler
        // instance; the transient container's cached executor would hand out fresh ones.
        var memoizedFirst = await DispatchAndReadId(memoizedEngine, memoizedContainer);
        var memoizedSecond = await DispatchAndReadId(memoizedEngine, memoizedContainer);
        Assert.Equal(memoizedFirst, memoizedSecond);

        // And after the slot moved on, the original container still dispatches through
        // its own transient-resolving executor.
        var third = await DispatchAndReadId(transientEngine, transientContainer);
        Assert.NotEqual(memoizedFirst, third);

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
            await engine.DispatchAsync(new UnregisteredCommand(), provider));
        var typed = await Record.ExceptionAsync(async () =>
            await engine.DispatchVoidAsync(new UnregisteredCommand(), provider));

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
        // second container would run the first one's pipeline — observable here through
        // handler lifetime, exactly as the void lane's twin asserts it.
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

        var memoizedFirst = await DispatchAndReadId(memoizedEngine, memoizedContainer);
        var memoizedSecond = await DispatchAndReadId(memoizedEngine, memoizedContainer);
        Assert.Equal(memoizedFirst, memoizedSecond);

        // And after the slot moved on, the original container still resolves transiently.
        var third = await DispatchAndReadId(transientEngine, transientContainer);
        Assert.NotEqual(memoizedFirst, third);

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
    public async Task TypedResultDispatch_ServesAMessageWithNoGeneratedRoot()
    {
        await using var provider = BuildProvider();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // A closed generic message has no root and never gets one, so the untyped lane
        // reaches DispatchLookup's reflective arm to build its executor — an answer only a
        // JIT can give. Naming the pair makes the closed executor type ordinary compiled
        // code, which is what lets this shape survive a Native AOT publish.
        //
        // Each lane gets its own container so each builds its own executor: sharing one
        // would let whichever ran first answer for both, which is correct at run time and
        // useless as a test of how the executor came to exist.
        var items = new ErgosfareContext();

        var typed = await engine.DispatchAsync<WrappedProbe<int>, string>(
            new WrappedProbe<int>(), items, provider);
        await using var fresh = BuildProvider();
        var untyped = await fresh.GetRequiredService<MessageDispatchEngine>()
            .DispatchAsync<string>(new WrappedProbe<int>(), fresh);

        Assert.Equal("wrapped", typed);
        Assert.Equal(typed, untyped);
    }
}
