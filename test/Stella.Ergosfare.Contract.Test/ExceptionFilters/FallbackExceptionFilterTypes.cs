using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Contract.Test.ExceptionFilters.Fallback;

// The same scenarios for the explicit runtime-registration axis. Excluded from discovery
// so the generator models nothing for them: no descriptor, no dispatch root, no staged
// plan — the exception stage is the reflective invocation strategy, which asks each
// resolved instance its filter probe instead of running a baked-in `is` test.

// --- void command: every exception-stage participant is filtered ------------

/// <inheritdoc cref="Generated.FilteredVoidCommand"/>
[ExcludeFromDiscovery]
public sealed class FilteredVoidCommand : IFilteredVoidCommand
{
    /// <inheritdoc />
    public required FaultKind Fault { get; init; }
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class FilteredVoidCommandHandler : FilteredVoidHandlerBase<FilteredVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class FilteredVoidCommandTagged : FilteredVoidTaggedBase<FilteredVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(1)]
public sealed class FilteredVoidCommandUnrelated : FilteredVoidUnrelatedBase<FilteredVoidCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class FilteredVoidCommandFinal : FilteredVoidFinalBase<FilteredVoidCommand>;

// --- string-result command: filtered beside unfiltered ----------------------

/// <inheritdoc cref="Generated.FilteredResultCommand"/>
[ExcludeFromDiscovery]
public sealed class FilteredResultCommand : IFilteredResultCommand
{
    /// <inheritdoc />
    public required FaultKind Fault { get; init; }
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class FilteredResultCommandHandler : FilteredResultHandlerBase<FilteredResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class FilteredResultCommandTagged : FilteredResultTaggedBase<FilteredResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(1)]
public sealed class FilteredResultCommandUntyped : FilteredResultUntypedBase<FilteredResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class FilteredResultCommandFinal : FilteredResultFinalBase<FilteredResultCommand>;
