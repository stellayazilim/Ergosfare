using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Sync.Fallback;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Sync;

/// <summary>
/// The same synchronous interceptor contract under explicit runtime registration. No
/// generated descriptor or plan exists for these types, so every stage resolves through
/// the reflective invocation strategies and their synchronous pattern-match arms.
/// </summary>
/// <remarks>
/// The ordering scenario registers each stage's slots out of weight order on purpose: if
/// registration order decided the sequence, the inherited expectation would fail here and
/// pass on the generated axis.
/// </remarks>
public sealed class RuntimeRegistrationSyncTests : SyncSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<SyncCommandHandler>()
                    .Register<SyncCommandPre>()
                    .Register<SyncCommandPost>()
                    .Register<SyncCommandException>()
                    .Register<SyncCommandFinal>()
                    .Register<SyncResultCommandHandler>()
                    .Register<SyncResultCommandPre>()
                    .Register<SyncResultCommandPost>()
                    .Register<SyncResultCommandException>()
                    .Register<SyncResultCommandFinal>()
                    .Register<SyncOrderedCommandHandler>()
                    .Register<OrderedAsyncLowPre>()
                    .Register<OrderedSyncMidPre>()
                    .Register<OrderedAsyncHighPre>()
                    .Register<OrderedAsyncLowPost>()
                    .Register<OrderedSyncHighPost>()
                    .Register<StaleKeyCommandHandler>()
                    .Register<StaleKeyCommandPost>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ISyncPayloadCommand NewCommand(string payload) => new SyncCommand { Payload = payload };

    /// <inheritdoc />
    protected override ISyncPayloadResultCommand NewResultCommand(string payload)
        => new SyncResultCommand { Payload = payload };

    /// <inheritdoc />
    protected override ISyncPayloadCommand NewOrderedCommand() => new SyncOrderedCommand();

    /// <inheritdoc />
    protected override ISyncPayloadCommand NewStaleKeyCommand() => new StaleKeyCommand();
}
