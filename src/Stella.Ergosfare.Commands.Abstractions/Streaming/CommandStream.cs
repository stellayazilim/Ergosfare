#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Streaming;

namespace Stella.Ergosfare.Commands.Abstractions.Streaming;

/// <summary>A command input stream. Declare metadata as properties on the concrete message.</summary>
/// <typeparam name="TChunk">The buffered item type.</typeparam>
/// <typeparam name="TSelf">The concrete message type.</typeparam>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public abstract class CommandStream<TChunk, TSelf>(int capacity = 24) : StreamInput<TChunk, TSelf>(capacity), ICommand
    where TSelf : CommandStream<TChunk, TSelf>;
