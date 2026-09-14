using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.Ergosfare.E2E.Contracts.Streaming;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class StreamingEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/streams", () => Results.Redirect("/streams.html"));
        app.MapGet("/streams/input", Collect);
        app.MapGet("/streams/duplex", Accumulate);
        app.MapGet("/streams/events", Watch);
    }

    private static async Task Collect(HttpContext http, ICommandMediator mediator)
    {
        if (!http.WebSockets.IsWebSocketRequest) { http.Response.StatusCode = 400; return; }
        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        try
        {
            await using var input = new CollectText().Pipe(ReadInput(socket, http.RequestAborted), http.RequestAborted);
            var text = await mediator.SendAsync<string>(input, http.RequestAborted);
            if (socket.State == WebSocketState.Open) await Send(socket, text, http.RequestAborted);
            await Finish(socket, http.RequestAborted);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
        catch (WebSocketException) { }
    }

    // These endpoints deliberately exercise the experimental stream API.
#pragma warning disable CS0618
    private static async Task Accumulate(HttpContext http, IQueryMediator mediator)
    {
        if (!http.WebSockets.IsWebSocketRequest) { http.Response.StatusCode = 400; return; }
        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        try
        {
            await using var query = new AccumulateText().Pipe(ReadInput(socket, http.RequestAborted), http.RequestAborted);
            await foreach (var text in mediator.StreamAsync(query, http.RequestAborted))
                if (socket.State == WebSocketState.Open) await Send(socket, text, http.RequestAborted);
            await Finish(socket, http.RequestAborted);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
        catch (WebSocketException) { }
    }

    private static async Task Watch(HttpContext http, IQueryMediator mediator)
    {
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        try
        {
            await foreach (var character in mediator.StreamAsync(new StreamGreeting(), http.RequestAborted))
            {
                var json = JsonSerializer.Serialize(character, ApiJsonContext.Default.String);
                await http.Response.WriteAsync($"event: character\ndata: {json}\n\n", http.RequestAborted);
                await http.Response.Body.FlushAsync(http.RequestAborted);
            }
            await http.Response.WriteAsync("event: done\ndata: {}\n\n", http.RequestAborted);
            await http.Response.Body.FlushAsync(http.RequestAborted);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
        catch (IOException) { http.Abort(); }
    }
#pragma warning restore CS0618

    private static async IAsyncEnumerable<string> ReadInput(WebSocket socket, [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[1024];
        var totalBytes = 0;
        while (socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult received;
            do
            {
                received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (received.MessageType == WebSocketMessageType.Close) yield break;
                totalBytes += received.Count;
                if (received.MessageType != WebSocketMessageType.Text || totalBytes > 16384)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, "Text only; maximum 16 KiB per input stream.", ct);
                    yield break;
                }
                message.Write(buffer, 0, received.Count);
            } while (!received.EndOfMessage);

            // A reserved frame completes input while keeping the output side open.
            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            if (text == "__END__") yield break;
            yield return text;
        }
    }

    private static Task Send(WebSocket socket, string text, CancellationToken ct)
        => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, ct);

    private static Task Finish(WebSocket socket, CancellationToken ct)
        => socket.State is WebSocketState.Open or WebSocketState.CloseReceived
            ? socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Completed", ct) : Task.CompletedTask;
}
