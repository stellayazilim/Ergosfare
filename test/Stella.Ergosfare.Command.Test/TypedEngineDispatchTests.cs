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

    public sealed class TypedProbeCommandHandler : ICommandHandler<TypedProbeCommand>
    {
        public ValueTask HandleAsync(TypedProbeCommand command, IExecutionContext context)
        {
            context.Set("typedProbe", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class UnregisteredCommand : ICommand { }

    public sealed class IdentityProbeCommand : ICommand { }

    public sealed class IdentityProbeCommandHandler : ICommandHandler<IdentityProbeCommand>
    {
        private readonly Guid _id = Guid.NewGuid();

        public ValueTask HandleAsync(IdentityProbeCommand command, IExecutionContext context)
        {
            context.Set("handlerId", _id);
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider BuildProvider()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<TypedProbeCommandHandler>()))
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
            var items = new Dictionary<object, object?>();

            await engine.DispatchVoidAsync(new TypedProbeCommand(), provider, items);

            Assert.Equal(true, items["typedProbe"]);
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
        var items = new Dictionary<object, object?>();

        await engine.DispatchVoidAsync(baseTyped, provider, items);

        Assert.Equal(true, items["typedProbe"]);
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
            var items = new Dictionary<object, object?>();
            await engine.DispatchVoidAsync(new IdentityProbeCommand(), provider, items);
            return Assert.IsType<Guid>(items["handlerId"]);
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
}
