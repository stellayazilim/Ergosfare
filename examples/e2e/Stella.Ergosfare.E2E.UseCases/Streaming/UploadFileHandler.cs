#pragma warning disable ERGOEXP003
using System.Security.Cryptography;
using System.Text.Json;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.E2E.Contracts.Streaming;

namespace Stella.Ergosfare.E2E.UseCases.Streaming;

public sealed record UploadStorage(string DirectoryPath);

public sealed class UploadFileHandler(UploadStorage storage) : ICommandHandler<UploadFile, UploadReceipt>
{
    public async ValueTask<UploadReceipt> HandleAsync(UploadFile message, ErgosfareContext context)
    {
        var session = context.Get<UploadSession>(UploadSession.ContextKey);
        Directory.CreateDirectory(storage.DirectoryPath);
        // Never use a client-controlled name as a filesystem path.
        var storedName = Guid.NewGuid().ToString("N") + ".upload";
        var path = Path.Combine(storage.DirectoryPath, storedName);
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long bytes = 0, chunks = 0;
            var count = session.FirstCount;
            while (count != 0)
            {
                // First iteration consumes the chunk read by pre; later iterations reuse its buffer.
                var chunk = session.Buffer.AsMemory(0, count);
                await file.WriteAsync(chunk, context.CancellationToken);
                hash.AppendData(chunk.Span);
                bytes += chunk.Length;
                chunks++;
                count = await session.File.Body.ReadAsync(session.Buffer, context.CancellationToken);
            }
            if (await session.Reader.ReadNextSectionAsync(context.CancellationToken) is not null)
                throw new UploadRejectedException("Send only one file per request.");
            await session.Body.CopyToAsync(Stream.Null, context.CancellationToken);
            await file.FlushAsync(context.CancellationToken);
            var receipt = new UploadReceipt(session.FileName, storedName, session.ContentType, bytes, chunks,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
            // MIME is transport metadata, not part of the file bytes. Preserve it separately.
            await using var metadata = new FileStream(path + ".json", FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous);
            await JsonSerializer.SerializeAsync(metadata, receipt, UploadMetadataJsonContext.Default.UploadReceipt,
                context.CancellationToken);
            return receipt;
        }
        catch
        {
            // A cancelled or malformed request must not leave a partial upload behind.
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            try { File.Delete(path + ".json"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }
}
