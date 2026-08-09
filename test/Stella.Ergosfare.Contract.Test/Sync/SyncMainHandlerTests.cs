using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Sync;

/// <summary>
/// The four synchronous main-handler contracts a dispatch can resolve — the bare-result and
/// the <see cref="ValueTask"/>-shaped form of both the void and the result pipeline. Each is
/// one arm of the handler pattern match the modernization removes, and each is exercised
/// twice: once without interceptors, where the executor invokes the handler itself, and once
/// with an interceptor, where the mediation strategy does.
/// </summary>
/// <remarks>
/// These types stay keyed on purpose. A synchronous main handler carries the bare result type
/// in its descriptor rather than the <c>ValueTask</c> carrier the plan computations require,
/// so no compile-time plan can ever serve it — both axes here reach the reflective path, and
/// the fallback axis is what pins the reflective loop the target architecture keeps. The
/// synchronous <em>interceptor</em> contracts, which do reach the emitted plans, live in
/// <see cref="SyncSemanticsContract"/>.
/// </remarks>
public abstract class SyncMainHandlerContract
{
    /// <summary>The discovery key this area registers under.</summary>
    protected const string Key = "contract.sync";

    /// <summary>Marks the handler and reports which contract served the dispatch.</summary>
    [ExcludeFromDiscovery]
    public abstract class VoidObjectHandlerBase<TCommand> : ICommand, IHandler<TCommand, object>
        where TCommand : class, ICommand
    {
        /// <inheritdoc />
        public object Handle(TCommand message, IExecutionContext context)
        {
            context.Mark("handler", "IHandler<T,object>");
            return "ignored";
        }
    }

    /// <inheritdoc cref="VoidObjectHandlerBase{TCommand}"/>
    [ExcludeFromDiscovery]
    public abstract class VoidTaskHandlerBase<TCommand> : ICommand, IHandler<TCommand, ValueTask>
        where TCommand : class, ICommand
    {
        /// <inheritdoc />
        public ValueTask Handle(TCommand message, IExecutionContext context)
        {
            context.Mark("handler", "IHandler<T,ValueTask>");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="VoidObjectHandlerBase{TCommand}"/>
    [ExcludeFromDiscovery]
    public abstract class ResultStringHandlerBase<TCommand> : ICommand, IHandler<TCommand, string>
        where TCommand : class, ICommand<string>
    {
        /// <inheritdoc />
        public string Handle(TCommand message, IExecutionContext context)
        {
            context.Mark("handler", "IHandler<T,string>");
            return "sync-value";
        }
    }

    /// <inheritdoc cref="VoidObjectHandlerBase{TCommand}"/>
    [ExcludeFromDiscovery]
    public abstract class ResultTaskHandlerBase<TCommand> : ICommand, IHandler<TCommand, ValueTask<string>>
        where TCommand : class, ICommand<string>
    {
        /// <inheritdoc />
        public ValueTask<string> Handle(TCommand message, IExecutionContext context)
        {
            context.Mark("handler", "IHandler<T,ValueTask<string>>");
            return ValueTask.FromResult("sync-task-value");
        }
    }

    /// <summary>Forces the interceptor-carrying path, where the strategy resolves the handler.</summary>
    [ExcludeFromDiscovery]
    public abstract class GatePreBase<TCommand> : ICommandPreInterceptor<TCommand>
        where TCommand : class, ICommand
    {
        /// <inheritdoc />
        public ValueTask<TCommand> HandleAsync(TCommand command, IExecutionContext context)
        {
            context.Mark("pre");
            return ValueTask.FromResult(command);
        }
    }

    /// <summary>A container with this axis' synchronous main handlers registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>Void command served by an <c>IHandler&lt;T, object&gt;</c>, no interceptors.</summary>
    protected abstract ICommand NewVoidObjectCommand();

    /// <summary>Void command served by an <c>IHandler&lt;T, ValueTask&gt;</c>, no interceptors.</summary>
    protected abstract ICommand NewVoidTaskCommand();

    /// <summary>String command served by an <c>IHandler&lt;T, string&gt;</c>, no interceptors.</summary>
    protected abstract ICommand<string> NewResultStringCommand();

    /// <summary>String command served by an <c>IHandler&lt;T, ValueTask&lt;string&gt;&gt;</c>, no interceptors.</summary>
    protected abstract ICommand<string> NewResultTaskCommand();

    /// <summary>Void command served by an <c>IHandler&lt;T, object&gt;</c> behind an interceptor.</summary>
    protected abstract ICommand NewInterceptedVoidCommand();

    /// <summary>String command served by an <c>IHandler&lt;T, string&gt;</c> behind an interceptor.</summary>
    protected abstract ICommand<string> NewInterceptedResultCommand();

    private PipelineRecorder NewRecorder() => new() { Label = GetType().Name };

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_void_handler_serves_a_dispatch()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewVoidObjectCommand(), recorder.Commands());

        recorder.AssertStages("handler");
        Assert.Equal("IHandler<T,object>", recorder.DetailOf("handler"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_ValueTask_shaped_synchronous_void_handler_serves_a_dispatch()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewVoidTaskCommand(), recorder.Commands());

        recorder.AssertStages("handler");
        Assert.Equal("IHandler<T,ValueTask>", recorder.DetailOf("handler"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_handler_returns_its_value_to_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultStringCommand(), recorder.Commands());

        recorder.AssertStages("handler");
        Assert.Equal("sync-value", result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_ValueTask_shaped_synchronous_handler_returns_its_value_to_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultTaskCommand(), recorder.Commands());

        recorder.AssertStages("handler");
        Assert.Equal("sync-task-value", result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_void_handler_serves_a_dispatch_that_carries_an_interceptor()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewInterceptedVoidCommand(), recorder.Commands());

        recorder.AssertStages("pre", "handler");
        Assert.Equal("IHandler<T,object>", recorder.DetailOf("handler"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_handler_behind_an_interceptor_still_returns_its_value()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewInterceptedResultCommand(), recorder.Commands());

        recorder.AssertStages("pre", "handler");
        Assert.Equal("sync-value", result);
    }
}

/// <summary>The synchronous main-handler types registered through generated descriptors.</summary>
public static class KeyedSyncMainTypes
{
    /// <summary>Void command served by a bare-result synchronous handler, no interceptors.</summary>
    [DiscoveryKey("contract.sync")]
    public sealed class VoidObjectCommand : ICommand;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class VoidObjectHandler : SyncMainHandlerContract.VoidObjectHandlerBase<VoidObjectCommand>;

    /// <summary>Void command served by a ValueTask-shaped synchronous handler.</summary>
    [DiscoveryKey("contract.sync")]
    public sealed class VoidTaskCommand : ICommand;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class VoidTaskHandler : SyncMainHandlerContract.VoidTaskHandlerBase<VoidTaskCommand>;

    /// <summary>String command served by a bare-result synchronous handler.</summary>
    [DiscoveryKey("contract.sync")]
    public sealed class ResultStringCommand : ICommand<string>;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class ResultStringHandler : SyncMainHandlerContract.ResultStringHandlerBase<ResultStringCommand>;

    /// <summary>String command served by a ValueTask-shaped synchronous handler.</summary>
    [DiscoveryKey("contract.sync")]
    public sealed class ResultTaskCommand : ICommand<string>;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class ResultTaskHandler : SyncMainHandlerContract.ResultTaskHandlerBase<ResultTaskCommand>;

    /// <summary>Void command whose synchronous handler sits behind an interceptor.</summary>
    [DiscoveryKey("contract.sync")]
    public sealed class InterceptedVoidCommand : ICommand;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class InterceptedVoidHandler : SyncMainHandlerContract.VoidObjectHandlerBase<InterceptedVoidCommand>;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class InterceptedVoidPre : SyncMainHandlerContract.GatePreBase<InterceptedVoidCommand>;

    /// <summary>String command whose synchronous handler sits behind an interceptor.</summary>
    [DiscoveryKey("contract.sync")]
    public sealed class InterceptedResultCommand : ICommand<string>;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class InterceptedResultHandler : SyncMainHandlerContract.ResultStringHandlerBase<InterceptedResultCommand>;

    /// <inheritdoc />
    [DiscoveryKey("contract.sync")]
    public sealed class InterceptedResultPre : SyncMainHandlerContract.GatePreBase<InterceptedResultCommand>;
}

/// <summary>The same shapes, hidden from the generator so <c>Register&lt;T&gt;()</c> is reflective.</summary>
public static class FallbackSyncMainTypes
{
    /// <inheritdoc cref="KeyedSyncMainTypes.VoidObjectCommand"/>
    [ExcludeFromDiscovery]
    public sealed class VoidObjectCommand : ICommand;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class VoidObjectHandler : SyncMainHandlerContract.VoidObjectHandlerBase<VoidObjectCommand>;

    /// <inheritdoc cref="KeyedSyncMainTypes.VoidTaskCommand"/>
    [ExcludeFromDiscovery]
    public sealed class VoidTaskCommand : ICommand;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class VoidTaskHandler : SyncMainHandlerContract.VoidTaskHandlerBase<VoidTaskCommand>;

    /// <inheritdoc cref="KeyedSyncMainTypes.ResultStringCommand"/>
    [ExcludeFromDiscovery]
    public sealed class ResultStringCommand : ICommand<string>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ResultStringHandler : SyncMainHandlerContract.ResultStringHandlerBase<ResultStringCommand>;

    /// <inheritdoc cref="KeyedSyncMainTypes.ResultTaskCommand"/>
    [ExcludeFromDiscovery]
    public sealed class ResultTaskCommand : ICommand<string>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ResultTaskHandler : SyncMainHandlerContract.ResultTaskHandlerBase<ResultTaskCommand>;

    /// <inheritdoc cref="KeyedSyncMainTypes.InterceptedVoidCommand"/>
    [ExcludeFromDiscovery]
    public sealed class InterceptedVoidCommand : ICommand;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedVoidHandler : SyncMainHandlerContract.VoidObjectHandlerBase<InterceptedVoidCommand>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedVoidPre : SyncMainHandlerContract.GatePreBase<InterceptedVoidCommand>;

    /// <inheritdoc cref="KeyedSyncMainTypes.InterceptedResultCommand"/>
    [ExcludeFromDiscovery]
    public sealed class InterceptedResultCommand : ICommand<string>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedResultHandler : SyncMainHandlerContract.ResultStringHandlerBase<InterceptedResultCommand>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedResultPre : SyncMainHandlerContract.GatePreBase<InterceptedResultCommand>;
}

/// <summary>The synchronous main-handler contract under generated (keyed) registration.</summary>
public sealed class GeneratedRegistrationSyncMainHandlerTests : SyncMainHandlerContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ICommand NewVoidObjectCommand() => new KeyedSyncMainTypes.VoidObjectCommand();

    /// <inheritdoc />
    protected override ICommand NewVoidTaskCommand() => new KeyedSyncMainTypes.VoidTaskCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewResultStringCommand() => new KeyedSyncMainTypes.ResultStringCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewResultTaskCommand() => new KeyedSyncMainTypes.ResultTaskCommand();

    /// <inheritdoc />
    protected override ICommand NewInterceptedVoidCommand() => new KeyedSyncMainTypes.InterceptedVoidCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewInterceptedResultCommand()
        => new KeyedSyncMainTypes.InterceptedResultCommand();
}

/// <summary>The same contract under explicit runtime registration.</summary>
public sealed class RuntimeRegistrationSyncMainHandlerTests : SyncMainHandlerContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<FallbackSyncMainTypes.VoidObjectHandler>()
                    .Register<FallbackSyncMainTypes.VoidTaskHandler>()
                    .Register<FallbackSyncMainTypes.ResultStringHandler>()
                    .Register<FallbackSyncMainTypes.ResultTaskHandler>()
                    .Register<FallbackSyncMainTypes.InterceptedVoidHandler>()
                    .Register<FallbackSyncMainTypes.InterceptedVoidPre>()
                    .Register<FallbackSyncMainTypes.InterceptedResultHandler>()
                    .Register<FallbackSyncMainTypes.InterceptedResultPre>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ICommand NewVoidObjectCommand() => new FallbackSyncMainTypes.VoidObjectCommand();

    /// <inheritdoc />
    protected override ICommand NewVoidTaskCommand() => new FallbackSyncMainTypes.VoidTaskCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewResultStringCommand() => new FallbackSyncMainTypes.ResultStringCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewResultTaskCommand() => new FallbackSyncMainTypes.ResultTaskCommand();

    /// <inheritdoc />
    protected override ICommand NewInterceptedVoidCommand() => new FallbackSyncMainTypes.InterceptedVoidCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewInterceptedResultCommand()
        => new FallbackSyncMainTypes.InterceptedResultCommand();
}
