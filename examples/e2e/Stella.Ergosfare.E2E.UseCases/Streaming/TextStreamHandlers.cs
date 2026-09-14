#pragma warning disable ERGOEXP003
using System.Text;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Streaming;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.E2E.UseCases.Streaming;

public sealed class CollectTextHandler : ICommandHandler<CollectText, string>
{
    public async ValueTask<string> HandleAsync(CollectText command, ErgosfareContext context)
    {
        var text = new StringBuilder();
        await foreach (var chunk in command.WithCancellation(context.CancellationToken))
            text.Append(chunk);
        return text.ToString();
    }
}

public sealed class AccumulateTextHandler : IStreamQueryHandler<AccumulateText, string>
{
    public async IAsyncEnumerable<string> StreamAsync(AccumulateText query, ErgosfareContext context)
    {
        var text = new StringBuilder();
        await foreach (var chunk in query.WithCancellation(context.CancellationToken))
        {
            text.Append(chunk);
            yield return text.ToString();
        }
    }
}

public sealed class StreamGreetingHandler : IStreamQueryHandler<StreamGreeting, string>
{
    public async IAsyncEnumerable<string> StreamAsync(StreamGreeting query, ErgosfareContext context)
    {
        foreach (var character in "Hello world")
        {
            await Task.Delay(150, context.CancellationToken);
            yield return character.ToString();
        }
    }
}
