#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Abstractions.Streaming;
using System.Text.Json.Serialization;

namespace Stella.Ergosfare.E2E.Contracts.Streaming;

public sealed class UploadFile(string? contentType, long? contentLength)
    : CommandStream<ReadOnlyMemory<byte>, UploadFile>(capacity: 4), ICommand<UploadReceipt>
{
    public string? ContentType { get; } = contentType;
    public long? ContentLength { get; } = contentLength;
}

// ContentType is the file section's declared MIME value, not the outer multipart type
// and not a server-side format detection result. Null means the header was absent.
public sealed record UploadReceipt(string FileName, string StoredName, string? ContentType, long Bytes, long Chunks, string Sha256);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UploadReceipt))]
public sealed partial class UploadMetadataJsonContext : JsonSerializerContext;
