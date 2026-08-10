using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Contract.Test.Sync.Generated;

// The synchronous-interceptor scenarios' types for the source-generated registration axis.
// They are top-level, unkeyed and ungrouped on purpose — the generator disqualifies keyed,
// grouped and nested participants from its compile-time plans, and these scenarios exist to
// run the emitted synchronous interceptor calls (`((IPreInterceptor<T>)x).Handle(...)` and
// friends). A [DiscoveryKey] here would still register, still pass, and silently test only
// what the fallback axis already covers.
//
// Every message type is local to this area and no interceptor is registered against a
// module marker, so joining the assembly's unkeyed pool cannot reach another area's
// pipelines.

// --- void command, synchronous interceptors --------------------------------

/// <summary>Void command whose interceptor stages are all synchronous.</summary>
public sealed class SyncCommand : ISyncPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class SyncCommandHandler : SyncVoidHandlerBase<SyncCommand>;

/// <inheritdoc />
public sealed class SyncCommandPre : SyncVoidPreBase<SyncCommand>;

/// <inheritdoc />
public sealed class SyncCommandPost : SyncVoidPostBase<SyncCommand>;

/// <inheritdoc />
public sealed class SyncCommandException : SyncVoidExceptionBase<SyncCommand>;

/// <inheritdoc />
public sealed class SyncCommandFinal : SyncVoidFinalBase<SyncCommand>;

// --- string-result command, synchronous interceptors -----------------------

/// <summary>String-result command whose interceptor stages are all synchronous.</summary>
public sealed class SyncResultCommand : ISyncPayloadResultCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class SyncResultCommandHandler : SyncResultHandlerBase<SyncResultCommand>;

/// <inheritdoc />
public sealed class SyncResultCommandPre : SyncResultPreBase<SyncResultCommand>;

/// <inheritdoc />
public sealed class SyncResultCommandPost : SyncResultPostBase<SyncResultCommand>;

/// <inheritdoc />
public sealed class SyncResultCommandException : SyncResultExceptionBase<SyncResultCommand>;

/// <inheritdoc />
public sealed class SyncResultCommandFinal : SyncResultFinalBase<SyncResultCommand>;

// --- stage ordering across the two flavors ---------------------------------

/// <summary>Void command whose stages interleave synchronous and asynchronous slots.</summary>
public sealed class SyncOrderedCommand : ISyncPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class SyncOrderedCommandHandler : SyncVoidHandlerBase<SyncOrderedCommand>;

/// <inheritdoc />
[Weight(8)]
public sealed class OrderedAsyncHighPre() : AsyncOrderedPreBase<SyncOrderedCommand>("pre:async-high");

/// <summary>Sits between the two asynchronous slots, so flavor cannot be what orders them.</summary>
[Weight(6)]
public sealed class OrderedSyncMidPre() : SyncOrderedPreBase<SyncOrderedCommand>("pre:sync-mid");

/// <inheritdoc />
[Weight(4)]
public sealed class OrderedAsyncLowPre() : AsyncOrderedPreBase<SyncOrderedCommand>("pre:async-low");

/// <inheritdoc />
[Weight(9)]
public sealed class OrderedSyncHighPost() : SyncOrderedPostBase<SyncOrderedCommand>("post:sync-high");

/// <inheritdoc />
[Weight(3)]
public sealed class OrderedAsyncLowPost() : AsyncOrderedPostBase<SyncOrderedCommand>("post:async-low");

// --- the pre-Unit result key -----------------------------------------------

/// <summary>Void command whose only post interceptor is still keyed on the old result type.</summary>
public sealed class StaleKeyCommand : ISyncPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class StaleKeyCommandHandler : SyncVoidHandlerBase<StaleKeyCommand>;

/// <inheritdoc />
public sealed class StaleKeyCommandPost : StaleKeyVoidPostBase<StaleKeyCommand>;
