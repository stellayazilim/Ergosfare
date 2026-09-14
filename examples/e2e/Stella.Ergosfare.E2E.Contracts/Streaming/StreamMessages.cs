// The E2E explicitly opts into the current experimental input-stream contract.
#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Commands.Abstractions.Streaming;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Abstractions.Streaming;

namespace Stella.Ergosfare.E2E.Contracts.Streaming;

public sealed class CollectText() : CommandStream<string, CollectText>(), ICommand<string>
{
    public string ContentType { get; init; } = "text/plain";
}

// The input stream is the message; IStreamQuery describes its streamed output.
public sealed class AccumulateText() : QueryStream<string, AccumulateText>(), IStreamQuery<string>;
public sealed record StreamGreeting : IStreamQuery<string>;
