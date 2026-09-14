using Stella.Ergosfare.Core.Abstractions.Attributes;

// The sub-namespace is load-bearing: this file and its Fallback twin declare the same
// type names on purpose, so one lane can be compared against the other. Flattening
// either to its folder namespace collides.
// ReSharper disable once CheckNamespace
namespace Stella.Ergosfare.Contract.Test.Abort.Generated;

// The post-abort scenarios' types for the source-generated registration axis. Top-level,
// unkeyed and ungrouped on purpose: the abort semantics the modernization is about to
// change live in the emitted staged-plan body (its catch-when/finally shell), and only
// default-discovery participants reach it. A [DiscoveryKey] here would register, pass, and
// silently test nothing the fallback axis does not already cover.
//
// Every message type is local to this area and no interceptor is registered against a
// module marker, so joining the assembly's unkeyed pool cannot reach another area's
// pipelines.

// --- void command -----------------------------------------------------------

/// <summary>Void command whose post stage aborts after the handler has run.</summary>
public sealed class AbortVoidCommand : IAbortCommand;

/// <inheritdoc />
public sealed class AbortVoidCommandHandler : AbortVoidHandlerBase<AbortVoidCommand>;

/// <inheritdoc />
[Weight(9)]
public sealed class AbortVoidCommandPost : AbortingVoidPostBase<AbortVoidCommand>;

/// <inheritdoc />
[Weight(1)]
public sealed class AbortVoidCommandLatePost : LateVoidPostBase<AbortVoidCommand>;

/// <inheritdoc />
public sealed class AbortVoidCommandException : AbortVoidExceptionBase<AbortVoidCommand>;

/// <inheritdoc />
public sealed class AbortVoidCommandFinal : AbortVoidFinalBase<AbortVoidCommand>;

// --- string-result command --------------------------------------------------

/// <summary>String-result command whose post stage aborts after the handler has run.</summary>
public sealed class AbortResultCommand : IAbortResultCommand;

/// <inheritdoc />
public sealed class AbortResultCommandHandler : AbortResultHandlerBase<AbortResultCommand>;

/// <inheritdoc />
[Weight(9)]
public sealed class AbortResultCommandPost : AbortingResultPostBase<AbortResultCommand>;

/// <inheritdoc />
[Weight(1)]
public sealed class AbortResultCommandLatePost : LateResultPostBase<AbortResultCommand>;

/// <inheritdoc />
public sealed class AbortResultCommandException : AbortResultExceptionBase<AbortResultCommand>;

/// <inheritdoc />
public sealed class AbortResultCommandFinal : AbortResultFinalBase<AbortResultCommand>;

// --- value-typed query ------------------------------------------------------

/// <summary>Value-typed query whose post stage aborts after the handler has run.</summary>
public sealed class AbortValueQuery : IAbortValueQuery;

/// <inheritdoc />
public sealed class AbortValueQueryHandler : AbortQueryHandlerBase<AbortValueQuery>;

/// <inheritdoc />
[Weight(9)]
public sealed class AbortValueQueryPost : AbortingQueryPostBase<AbortValueQuery>;

/// <inheritdoc />
[Weight(1)]
public sealed class AbortValueQueryLatePost : LateQueryPostBase<AbortValueQuery>;

/// <inheritdoc />
public sealed class AbortValueQueryException : AbortQueryExceptionBase<AbortValueQuery>;

/// <inheritdoc />
public sealed class AbortValueQueryFinal : AbortQueryFinalBase<AbortValueQuery>;
