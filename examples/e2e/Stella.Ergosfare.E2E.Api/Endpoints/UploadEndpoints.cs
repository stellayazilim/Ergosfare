#pragma warning disable ERGOEXP003
using Microsoft.AspNetCore.Http.Features;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.E2E.Api.Contracts;
using Stella.Ergosfare.E2E.Contracts.Streaming;
using Stella.Ergosfare.E2E.UseCases.Streaming;
using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

public sealed class UploadEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/streams/upload", (IWebHostEnvironment env) =>
            Results.File(Path.Combine(env.WebRootPath, "upload.html"), "text/html; charset=utf-8"));
        app.MapPost("/streams/upload", Upload);
    }

    private static async Task<IResult> Upload(HttpContext http, ICommandMediator mediator)
    {
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = UploadLimits.MaxRequestBytes;

        // Pipe the raw body. The application pipeline owns multipart parsing and validation.
        await using var input = new UploadFile(http.Request.ContentType, http.Request.ContentLength)
            .Pipe(http.Request.Body, chunkSize: 65536, cancellationToken: http.RequestAborted);
        try
        {
            var receipt = await mediator.SendAsync<UploadReceipt>(input, http.RequestAborted);
            return Results.Json(receipt, ApiJsonContext.Default.UploadReceipt);
        }
        catch (UploadRejectedException error) { return Results.Text(error.Message, statusCode: error.StatusCode); }
        catch (InvalidDataException) { return Results.Text("The multipart body is invalid or exceeds the limit.", statusCode: 400); }
        catch (IOException) { return Results.Text("The upload stream could not be completed.", statusCode: 400); }
    }
}
