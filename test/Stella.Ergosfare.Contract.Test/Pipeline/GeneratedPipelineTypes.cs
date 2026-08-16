using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Contract.Test.Pipeline;

// The pipeline scenarios' types for the source-generated registration axis. They are the
// assembly's ONLY default-discovery constructs — every other area's types carry a
// [DiscoveryKey] or [ExcludeFromDiscovery] — so the pattern-less RegisterGenerated()
// selects exactly this set.
//
// The default key is not a convenience here, it is the point: the generator disqualifies
// keyed, grouped and nested types from its compile-time plans, so a keyed variant of this
// axis would register through generated descriptors and still dispatch on the reflective
// executors, testing nothing the fallback axis does not already cover. Keep these types
// top-level, unkeyed and ungrouped, or the axis silently stops exercising the plan lanes.

// --- void command, full pipeline -------------------------------------------

/// <summary>Void command whose pipeline carries every interceptor stage.</summary>
public sealed class PipelineCommand : IPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class PipelineCommandHandler : PayloadHandlerBase<PipelineCommand>;

/// <inheritdoc />
public sealed class PipelineCommandPre : PayloadPreBase<PipelineCommand>;

/// <inheritdoc />
public sealed class PipelineCommandPost : PayloadPostBase<PipelineCommand>;

/// <inheritdoc />
public sealed class PipelineCommandException : PayloadExceptionBase<PipelineCommand>;

/// <inheritdoc />
public sealed class PipelineCommandFinal : PayloadFinalBase<PipelineCommand>;

// --- void command, handler only --------------------------------------------

/// <summary>Void command with nothing but a handler, for unhandled-exception semantics.</summary>
public sealed class BarePipelineCommand : IPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class BarePipelineCommandHandler : PayloadHandlerBase<BarePipelineCommand>;

// --- string-result command, full pipeline ----------------------------------

/// <summary>String-result command whose pipeline carries every interceptor stage.</summary>
public sealed class PipelineResultCommand : IPayloadResultCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class PipelineResultCommandHandler : PayloadResultHandlerBase<PipelineResultCommand>;

/// <inheritdoc />
public sealed class PipelineResultCommandPre : PayloadResultPreBase<PipelineResultCommand>;

/// <inheritdoc />
public sealed class PipelineResultCommandPost : PayloadResultPostBase<PipelineResultCommand>;

/// <inheritdoc />
public sealed class PipelineResultCommandException : PayloadResultExceptionBase<PipelineResultCommand>;

/// <inheritdoc />
public sealed class PipelineResultCommandFinal : PayloadResultFinalBase<PipelineResultCommand>;

// --- string-result command, handler only -----------------------------------

/// <summary>String-result command with nothing but a handler.</summary>
public sealed class BarePipelineResultCommand : IPayloadResultCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class BarePipelineResultCommandHandler : PayloadResultHandlerBase<BarePipelineResultCommand>;

// --- value-typed query, full pipeline --------------------------------------

/// <summary>Value-typed query whose pipeline carries every interceptor stage.</summary>
public sealed class PipelineValueQuery : IPayloadValueQuery
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class PipelineValueQueryHandler : PayloadValueHandlerBase<PipelineValueQuery>;

/// <inheritdoc />
public sealed class PipelineValueQueryPre : PayloadValuePreBase<PipelineValueQuery>;

/// <inheritdoc />
public sealed class PipelineValueQueryPost : PayloadValuePostBase<PipelineValueQuery>;

/// <inheritdoc />
public sealed class PipelineValueQueryException : PayloadValueExceptionBase<PipelineValueQuery>;

/// <inheritdoc />
public sealed class PipelineValueQueryFinal : PayloadValueFinalBase<PipelineValueQuery>;

// --- value-typed query, handler only ---------------------------------------

/// <summary>Value-typed query with nothing but a handler.</summary>
public sealed class BarePipelineValueQuery : IPayloadValueQuery
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class BarePipelineValueQueryHandler : PayloadValueHandlerBase<BarePipelineValueQuery>;

// --- stage ordering ---------------------------------------------------------

/// <summary>Void command whose pipeline exists only to expose stage ordering.</summary>
public sealed class OrderedCommand : ICommand;

/// <inheritdoc />
public sealed class OrderedCommandHandler : OrderedHandlerBase<OrderedCommand>;

/// <inheritdoc />
[Weight(7)]
public sealed class OrderedHeavyPre() : OrderedPreBase<OrderedCommand>("pre:heavy");

/// <inheritdoc />
[Weight(2)]
public sealed class OrderedLightPre() : OrderedPreBase<OrderedCommand>("pre:light");

/// <summary>Shares <see cref="OrderedTieOmegaPre"/>'s weight; its type name sorts first.</summary>
[Weight(5)]
public sealed class OrderedTieAlphaPre() : OrderedPreBase<OrderedCommand>("pre:tie-alpha");

/// <summary>Shares <see cref="OrderedTieAlphaPre"/>'s weight; its type name sorts last.</summary>
[Weight(5)]
public sealed class OrderedTieOmegaPre() : OrderedPreBase<OrderedCommand>("pre:tie-omega");

/// <inheritdoc />
[Weight(9)]
public sealed class OrderedHighPost() : OrderedPostBase<OrderedCommand>("post:high");

/// <inheritdoc />
[Weight(3)]
public sealed class OrderedLowPost() : OrderedPostBase<OrderedCommand>("post:low");

// --- void command, async typed interceptors ---------------------------------

/// <summary>Void command whose post, exception and final stages bind async typed over <c>Unit</c>.</summary>
public sealed class AsyncTypedPipelineCommand : IPayloadCommand
{
    /// <inheritdoc />
    public string Payload { get; set; } = string.Empty;
}

/// <inheritdoc />
public sealed class AsyncTypedPipelineCommandHandler : PayloadHandlerBase<AsyncTypedPipelineCommand>;

/// <inheritdoc />
public sealed class AsyncTypedPipelineCommandPost : AsyncTypedPostBase<AsyncTypedPipelineCommand>;

/// <inheritdoc />
public sealed class AsyncTypedPipelineCommandException : AsyncTypedExceptionBase<AsyncTypedPipelineCommand>;

/// <inheritdoc />
public sealed class AsyncTypedPipelineCommandFinal : AsyncTypedFinalBase<AsyncTypedPipelineCommand>;
