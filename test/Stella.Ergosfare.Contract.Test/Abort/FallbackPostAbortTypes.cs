using Stella.Ergosfare.Core.Abstractions.Attributes;

// The post-abort scenarios' types for the runtime-registration axis. Every type here is
// excluded from discovery, so the generator models none of them: no pre-computed
// descriptors and no staged plan, and the scenarios abort inside the reflective mediation
// strategy instead of the emitted plan body. Excluding them is mandatory, not tidiness —
// the generator's descriptor catalog is filled by a module initializer for every type it
// models, so a discoverable type would keep its pre-computed descriptors even when
// registered with Register<T>().
// The sub-namespace is load-bearing: this file and its Generated twin declare the same
// type names on purpose, so one lane can be compared against the other. Flattening
// either to its folder namespace collides.
// ReSharper disable once CheckNamespace
namespace Stella.Ergosfare.Contract.Test.Abort.Fallback;

// --- void command -----------------------------------------------------------

/// <inheritdoc cref="Generated.AbortVoidCommand"/>
[ExcludeFromDiscovery]
public sealed class AbortVoidCommand : IAbortCommand;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortVoidCommandHandler : AbortVoidHandlerBase<AbortVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class AbortVoidCommandPost : AbortingVoidPostBase<AbortVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(1)]
public sealed class AbortVoidCommandLatePost : LateVoidPostBase<AbortVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortVoidCommandException : AbortVoidExceptionBase<AbortVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortVoidCommandFinal : AbortVoidFinalBase<AbortVoidCommand>;

// --- string-result command --------------------------------------------------

/// <inheritdoc cref="Generated.AbortResultCommand"/>
[ExcludeFromDiscovery]
public sealed class AbortResultCommand : IAbortResultCommand;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortResultCommandHandler : AbortResultHandlerBase<AbortResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class AbortResultCommandPost : AbortingResultPostBase<AbortResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(1)]
public sealed class AbortResultCommandLatePost : LateResultPostBase<AbortResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortResultCommandException : AbortResultExceptionBase<AbortResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortResultCommandFinal : AbortResultFinalBase<AbortResultCommand>;

// --- value-typed query ------------------------------------------------------

/// <inheritdoc cref="Generated.AbortValueQuery"/>
[ExcludeFromDiscovery]
public sealed class AbortValueQuery : IAbortValueQuery;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortValueQueryHandler : AbortQueryHandlerBase<AbortValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class AbortValueQueryPost : AbortingQueryPostBase<AbortValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(1)]
public sealed class AbortValueQueryLatePost : LateQueryPostBase<AbortValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortValueQueryException : AbortQueryExceptionBase<AbortValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AbortValueQueryFinal : AbortQueryFinalBase<AbortValueQuery>;
