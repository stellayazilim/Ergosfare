// The E2E explicitly opts into the current experimental input-stream contract.
#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Commands.Abstractions.Streaming;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.Contracts.Streaming;

public sealed class CollectText(IAsyncEnumerable<string> input)
    : ErgosfareCommandStream<string, string, string>("text/plain", input);

// The input stream is the message; IStreamQuery describes its streamed output.
public sealed class AccumulateText(IAsyncEnumerable<string> input)
    : ErgosfareStream<string>(input), IStreamQuery<string>;
public sealed record StreamGreeting : IStreamQuery<string>;
