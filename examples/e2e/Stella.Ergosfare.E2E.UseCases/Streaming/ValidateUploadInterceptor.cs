#pragma warning disable ERGOEXP003
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.E2E.Contracts.Streaming;

namespace Stella.Ergosfare.E2E.UseCases.Streaming;

public static class UploadLimits
{
    public const long MaxRequestBytes = 2L * 1024 * 1024 * 1024;
}

public sealed class UploadRejectedException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// Dispatch-local parser state and the first payload chunk. The input is never re-enumerated.
internal sealed record UploadSession(Stream Body, MultipartReader Reader, MultipartSection File,
    string FileName, string? ContentType, byte[] Buffer, int FirstCount)
{
    public const string ContextKey = "E2E.UploadSession";
}

public sealed class ValidateUploadInterceptor : ICommandPreInterceptor<UploadFile>
{
    public async ValueTask<UploadFile> HandleAsync(UploadFile message, ErgosfareContext context)
    {
        if (!MediaTypeHeaderValue.TryParse(message.ContentType, out var contentType)
            || !string.Equals(contentType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new UploadRejectedException("Expected multipart/form-data.", 415);
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 128)
            throw new UploadRejectedException("A valid multipart boundary is required.");
        if (message.ContentLength > UploadLimits.MaxRequestBytes)
            throw new UploadRejectedException("The request limit is 2 GiB.", 413);

        var body = message.AsStream();
        try
        {
            var reader = new MultipartReader(boundary, body, bufferSize: 65536)
                { BodyLengthLimit = UploadLimits.MaxRequestBytes };
            var section = await reader.ReadNextSectionAsync(context.CancellationToken);
            if (section is null || !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || disposition.DispositionType != "form-data"
                || HeaderUtilities.RemoveQuotes(disposition.Name).Value != "file"
                || (!disposition.FileName.HasValue && !disposition.FileNameStar.HasValue))
                throw new UploadRejectedException("Send a single file in the 'file' field.");

            var suppliedName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue
                ? disposition.FileNameStar : disposition.FileName).Value ?? "upload";
            var fileName = Path.GetFileName(suppliedName.Replace('\\', '/'));
            var buffer = new byte[65536];
            var firstCount = await section.Body.ReadAsync(buffer, context.CancellationToken);
            // A zero-length file is valid. Missing file sections are rejected above.
            context.Set(UploadSession.ContextKey, new UploadSession(body, reader, section, fileName,
                section.ContentType, buffer, firstCount));
            return message;
        }
        catch
        {
            try { await body.DisposeAsync(); } catch { /* Preserve the validation or parser failure. */ }
            throw;
        }
    }
}

public sealed class DisposeUploadInterceptor : ICommandFinalInterceptor<UploadFile, UploadReceipt>
{
    public async ValueTask HandleAsync(UploadFile message, UploadReceipt? result, Exception? exception, ErgosfareContext context)
    {
        if (!context.TryGet<UploadSession>(UploadSession.ContextKey, out var session)) return;
        context.Items.Remove(UploadSession.ContextKey);
        try { await session.Body.DisposeAsync(); }
        catch when (exception is not null) { /* Cleanup must not replace the primary failure. */ }
    }
}
