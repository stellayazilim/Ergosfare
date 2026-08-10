using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Contract.Test.ExceptionFilters.Generated;

// The typed exception-interceptor types for the source-generated registration axis.
// Top-level, unkeyed and ungrouped on purpose: the filter has to be baked into the emitted
// staged-plan body as a compile-time `is` guard, and only default-discovery participants
// reach one. A [DiscoveryKey] here would register, pass, and silently prove nothing the
// fallback axis does not already cover.
//
// Every message type is local to this area and no interceptor is registered against a
// module marker, so joining the assembly's unkeyed pool cannot reach another area's
// pipelines — and no other area's exception can reach these filters.

// --- void command: every exception-stage participant is filtered ------------

/// <summary>Void command whose handler throws the fault it carries.</summary>
public sealed class FilteredVoidCommand : IFilteredVoidCommand
{
    /// <inheritdoc />
    public required FaultKind Fault { get; init; }
}

/// <inheritdoc />
public sealed class FilteredVoidCommandHandler : FilteredVoidHandlerBase<FilteredVoidCommand>;

/// <inheritdoc />
[Weight(9)]
public sealed class FilteredVoidCommandTagged : FilteredVoidTaggedBase<FilteredVoidCommand>;

/// <inheritdoc />
[Weight(1)]
public sealed class FilteredVoidCommandUnrelated : FilteredVoidUnrelatedBase<FilteredVoidCommand>;

/// <inheritdoc />
public sealed class FilteredVoidCommandFinal : FilteredVoidFinalBase<FilteredVoidCommand>;

// --- string-result command: filtered beside unfiltered ----------------------

/// <summary>String-result command whose handler throws the fault it carries.</summary>
public sealed class FilteredResultCommand : IFilteredResultCommand
{
    /// <inheritdoc />
    public required FaultKind Fault { get; init; }
}

/// <inheritdoc />
public sealed class FilteredResultCommandHandler : FilteredResultHandlerBase<FilteredResultCommand>;

/// <inheritdoc />
[Weight(9)]
public sealed class FilteredResultCommandTagged : FilteredResultTaggedBase<FilteredResultCommand>;

/// <inheritdoc />
[Weight(1)]
public sealed class FilteredResultCommandUntyped : FilteredResultUntypedBase<FilteredResultCommand>;

/// <inheritdoc />
public sealed class FilteredResultCommandFinal : FilteredResultFinalBase<FilteredResultCommand>;
