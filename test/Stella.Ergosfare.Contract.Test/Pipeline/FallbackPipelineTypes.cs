using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;

// The pipeline scenarios' types for the runtime-registration axis — the fallback the
// generated axis degrades to. Every type here is excluded from discovery, so the source
// generator models none of them: no pre-computed descriptors, no dispatch roots, no
// compile-time plans. The scenarios therefore exercise the reflective registration path
// end to end, and any behavioral drift between it and the generated path shows up as one
// axis failing while the other passes.
// The sub-namespace is load-bearing: this file and its generated twin declare the same
// type names on purpose, so one lane can be compared against the other. Flattening
// either to its folder namespace collides.
// ReSharper disable once CheckNamespace
namespace Stella.Ergosfare.Contract.Test.Pipeline.Fallback;

// --- void command, full pipeline -------------------------------------------

/// <inheritdoc cref="Pipeline.PipelineCommand"/>
[ExcludeFromDiscovery]
public sealed class PipelineCommand : IPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineCommandHandler : PayloadHandlerBase<PipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineCommandPre : PayloadPreBase<PipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineCommandPost : PayloadPostBase<PipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineCommandException : PayloadExceptionBase<PipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineCommandFinal : PayloadFinalBase<PipelineCommand>;

// --- void command, handler only --------------------------------------------

/// <inheritdoc cref="Pipeline.BarePipelineCommand"/>
[ExcludeFromDiscovery]
public sealed class BarePipelineCommand : IPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class BarePipelineCommandHandler : PayloadHandlerBase<BarePipelineCommand>;

// --- string-result command, full pipeline ----------------------------------

/// <inheritdoc cref="Pipeline.PipelineResultCommand"/>
[ExcludeFromDiscovery]
public sealed class PipelineResultCommand : IPayloadResultCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineResultCommandHandler : PayloadResultHandlerBase<PipelineResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineResultCommandPre : PayloadResultPreBase<PipelineResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineResultCommandPost : PayloadResultPostBase<PipelineResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineResultCommandException : PayloadResultExceptionBase<PipelineResultCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineResultCommandFinal : PayloadResultFinalBase<PipelineResultCommand>;

// --- string-result command, handler only -----------------------------------

/// <inheritdoc cref="Pipeline.BarePipelineResultCommand"/>
[ExcludeFromDiscovery]
public sealed class BarePipelineResultCommand : IPayloadResultCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class BarePipelineResultCommandHandler : PayloadResultHandlerBase<BarePipelineResultCommand>;

// --- value-typed query, full pipeline --------------------------------------

/// <inheritdoc cref="Pipeline.PipelineValueQuery"/>
[ExcludeFromDiscovery]
public sealed class PipelineValueQuery : IPayloadValueQuery
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineValueQueryHandler : PayloadValueHandlerBase<PipelineValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineValueQueryPre : PayloadValuePreBase<PipelineValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineValueQueryPost : PayloadValuePostBase<PipelineValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineValueQueryException : PayloadValueExceptionBase<PipelineValueQuery>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class PipelineValueQueryFinal : PayloadValueFinalBase<PipelineValueQuery>;

// --- value-typed query, handler only ---------------------------------------

/// <inheritdoc cref="Pipeline.BarePipelineValueQuery"/>
[ExcludeFromDiscovery]
public sealed class BarePipelineValueQuery : IPayloadValueQuery
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class BarePipelineValueQueryHandler : PayloadValueHandlerBase<BarePipelineValueQuery>;

// --- stage ordering ---------------------------------------------------------

/// <inheritdoc cref="Pipeline.OrderedCommand"/>
[ExcludeFromDiscovery]
public sealed class OrderedCommand : ICommand;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class OrderedCommandHandler : OrderedHandlerBase<OrderedCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(7)]
public sealed class OrderedHeavyPre() : OrderedPreBase<OrderedCommand>("pre:heavy");

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(2)]
public sealed class OrderedLightPre() : OrderedPreBase<OrderedCommand>("pre:light");

/// <summary>Shares <see cref="OrderedTieOmegaPre"/>'s weight; its type name sorts first.</summary>
[ExcludeFromDiscovery]
[Weight(5)]
public sealed class OrderedTieAlphaPre() : OrderedPreBase<OrderedCommand>("pre:tie-alpha");

/// <summary>Shares <see cref="OrderedTieAlphaPre"/>'s weight; its type name sorts last.</summary>
[ExcludeFromDiscovery]
[Weight(5)]
public sealed class OrderedTieOmegaPre() : OrderedPreBase<OrderedCommand>("pre:tie-omega");

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(9)]
public sealed class OrderedHighPost() : OrderedPostBase<OrderedCommand>("post:high");

/// <inheritdoc />
[ExcludeFromDiscovery]
[Weight(3)]
public sealed class OrderedLowPost() : OrderedPostBase<OrderedCommand>("post:low");

// --- void command, async typed interceptors ---------------------------------

/// <inheritdoc cref="Pipeline.AsyncTypedPipelineCommand"/>
[ExcludeFromDiscovery]
public sealed class AsyncTypedPipelineCommand : IPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AsyncTypedPipelineCommandHandler : PayloadHandlerBase<AsyncTypedPipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AsyncTypedPipelineCommandPost : AsyncTypedPostBase<AsyncTypedPipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AsyncTypedPipelineCommandException : AsyncTypedExceptionBase<AsyncTypedPipelineCommand>;

/// <inheritdoc />
[ExcludeFromDiscovery]
public sealed class AsyncTypedPipelineCommandFinal : AsyncTypedFinalBase<AsyncTypedPipelineCommand>;
