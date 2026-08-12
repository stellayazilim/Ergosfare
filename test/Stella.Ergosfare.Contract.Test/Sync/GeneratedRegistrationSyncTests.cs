using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Sync.Generated;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Sync;

/// <summary>
/// The synchronous interceptor contract under source-generated registration. The generator
/// emits the staged plans for these messages, so the scenarios run the emitted
/// <c>((IPreInterceptor&lt;T&gt;)x).Handle(...)</c> calls rather than the reflective
/// invocation strategies.
/// </summary>
/// <remarks>
/// The pattern-less overload is deliberate: plan eligibility requires default-discovery,
/// top-level, ungrouped participants, so a keyed selection would quietly fall back to the
/// reflective arms the other axis already covers.
/// </remarks>
public sealed class GeneratedRegistrationSyncTests : SyncSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated()))
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
