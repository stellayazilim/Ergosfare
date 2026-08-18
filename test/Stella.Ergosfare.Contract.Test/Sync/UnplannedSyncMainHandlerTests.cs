using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Sync;

// The four synchronous main-handler shapes. The bare-result pair is top-level and unkeyed
// so the generator sees each one and declines it. The ValueTask-shaped pair CANNOT be:
// suspicious behavior 10 still holds on the rewritten engine — an unkeyed
// IHandler<T, ValueTask> / IHandler<T, ValueTask<TResult>> makes the generator emit
// AddVoidPlan/AddResultPlan constrained to the asynchronous contracts, and the emitted
// file fails to compile with CS0311 (verified again on this branch). A compile error
// cannot be pinned by a test, so those two stay excluded from discovery and reach the
// same throw through hand registration.
//
// Every handler carries the ICommand marker itself: the module builders reject any type
// without it, and the bare synchronous contracts have no module-flavored facade to
// inherit it from (suite README, suspicious behavior 9).

/// <summary>Void command served by a bare-result synchronous handler.</summary>
public sealed class SyncVoidObjectCommand : ICommand;

/// <inheritdoc cref="SyncVoidObjectCommand"/>
public sealed class SyncVoidObjectHandler : ICommand, IHandler<SyncVoidObjectCommand, object>
{
    /// <inheritdoc />
    public object Handle(SyncVoidObjectCommand message, ErgosfareContext context) => "ignored";
}

/// <summary>Void command served by a ValueTask-shaped synchronous handler.</summary>
[ExcludeFromDiscovery]
public sealed class SyncVoidTaskCommand : ICommand;

/// <inheritdoc cref="SyncVoidTaskCommand"/>
[ExcludeFromDiscovery]
public sealed class SyncVoidTaskHandler : ICommand, IHandler<SyncVoidTaskCommand, ValueTask>
{
    /// <inheritdoc />
    public ValueTask Handle(SyncVoidTaskCommand message, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>String command served by a bare-result synchronous handler.</summary>
public sealed class SyncResultStringCommand : ICommand<string>;

/// <inheritdoc cref="SyncResultStringCommand"/>
public sealed class SyncResultStringHandler : ICommand, IHandler<SyncResultStringCommand, string>
{
    /// <inheritdoc />
    public string Handle(SyncResultStringCommand message, ErgosfareContext context) => "sync-value";
}

/// <summary>String command served by a ValueTask-shaped synchronous handler.</summary>
[ExcludeFromDiscovery]
public sealed class SyncResultTaskCommand : ICommand<string>;

/// <inheritdoc cref="SyncResultTaskCommand"/>
[ExcludeFromDiscovery]
public sealed class SyncResultTaskHandler : ICommand, IHandler<SyncResultTaskCommand, ValueTask<string>>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(SyncResultTaskCommand message, ErgosfareContext context)
        => ValueTask.FromResult("sync-task-value");
}

/// <summary>
/// The synchronous main-handler feature is dead: no compiled plan can call any of the four
/// shapes — the bare-result contracts put the bare result type in their descriptor rather
/// than the <c>ValueTask</c> carrier every plan matches against, and the emitted plan
/// bodies constrain their handlers to the asynchronous contracts — and with the runtime
/// lane removed there is nothing left to degrade to. Every dispatch fails loudly, and the
/// reason says why. Synchronous <em>interceptors</em> are unaffected: they reach the
/// emitted plans, and <see cref="SyncSemanticsContract"/> keeps pinning them.
/// </summary>
public sealed class UnplannedSyncMainHandlerTests
{
    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<SyncVoidObjectHandler>()
                    .Register<SyncVoidTaskHandler>()
                    .Register<SyncResultStringHandler>()
                    .Register<SyncResultTaskHandler>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_bare_result_synchronous_void_handler_cannot_be_dispatched()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new SyncVoidObjectCommand()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Equal(typeof(SyncVoidObjectCommand), thrown.MessageType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_ValueTask_shaped_synchronous_void_handler_cannot_be_dispatched()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new SyncVoidTaskCommand()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_bare_result_synchronous_handler_cannot_be_dispatched()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new SyncResultStringCommand()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_ValueTask_shaped_synchronous_result_handler_cannot_be_dispatched()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new SyncResultTaskCommand()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
    }
}
