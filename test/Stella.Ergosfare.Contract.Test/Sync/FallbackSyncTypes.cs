using Stella.Ergosfare.Core.Abstractions.Attributes;

// The synchronous-interceptor scenarios' types for the runtime-registration axis. Every
// type here is excluded from discovery, so the generator models none of them: no
// pre-computed descriptors and no compile-time plan, and the scenarios reach the
// synchronous arms of the reflective invocation strategies instead of the emitted calls.
// Excluding them is mandatory, not tidiness — the generator's descriptor catalog is filled
// by a module initializer for every type it models, so a discoverable type would keep its
// pre-computed descriptors even when registered with Register<T>().
namespace Stella.Ergosfare.Contract.Test.Sync.Fallback;

// --- void command, synchronous interceptors --------------------------------

/// <inheritdoc cref="Generated.SyncCommand"/>
[ExcludeFromDiscovery]
public sealed class SyncCommand : ISyncPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncCommandHandler : SyncVoidHandlerBase<SyncCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncCommandPre : SyncVoidPreBase<SyncCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncCommandPost : SyncVoidPostBase<SyncCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncCommandException : SyncVoidExceptionBase<SyncCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncCommandFinal : SyncVoidFinalBase<SyncCommand>;

// --- string-result command, synchronous interceptors -----------------------

/// <inheritdoc cref="Generated.SyncResultCommand"/>
[ExcludeFromDiscovery]
public sealed class SyncResultCommand : ISyncPayloadResultCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncResultCommandHandler : SyncResultHandlerBase<SyncResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncResultCommandPre : SyncResultPreBase<SyncResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncResultCommandPost : SyncResultPostBase<SyncResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncResultCommandException : SyncResultExceptionBase<SyncResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncResultCommandFinal : SyncResultFinalBase<SyncResultCommand>;

// --- stage ordering across the two flavors ---------------------------------

/// <inheritdoc cref="Generated.SyncOrderedCommand"/>
[ExcludeFromDiscovery]
public sealed class SyncOrderedCommand : ISyncPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class SyncOrderedCommandHandler : SyncVoidHandlerBase<SyncOrderedCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(8)]
public sealed class OrderedAsyncHighPre() : AsyncOrderedPreBase<SyncOrderedCommand>("pre:async-high");

/// <inheritdoc cref="Generated.OrderedSyncMidPre"/>
[ExcludeFromDiscovery]
[Weight(6)]
public sealed class OrderedSyncMidPre() : SyncOrderedPreBase<SyncOrderedCommand>("pre:sync-mid");

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(4)]
public sealed class OrderedAsyncLowPre() : AsyncOrderedPreBase<SyncOrderedCommand>("pre:async-low");

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class OrderedSyncHighPost() : SyncOrderedPostBase<SyncOrderedCommand>("post:sync-high");

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(3)]
public sealed class OrderedAsyncLowPost() : AsyncOrderedPostBase<SyncOrderedCommand>("post:async-low");

// --- the pre-Unit result key -----------------------------------------------

/// <inheritdoc cref="Generated.StaleKeyCommand"/>
[ExcludeFromDiscovery]
public sealed class StaleKeyCommand : ISyncPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class StaleKeyCommandHandler : SyncVoidHandlerBase<StaleKeyCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class StaleKeyCommandPost : StaleKeyVoidPostBase<StaleKeyCommand>;
