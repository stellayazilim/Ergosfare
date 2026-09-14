#pragma warning disable ERGOEXP003
using System.Runtime.CompilerServices;
using System.Text;

namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>Reusable converters for caller-owned streams. Converters never close the source.</summary>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public static class PipeConverters
{
    /// <summary>Reads individual bytes. Prefer byte blocks for bulk transfers.</summary>
    public static IPipeConverter<byte> Bytes { get; } = new ByteConverter();

    /// <summary>Reads independently owned byte blocks.</summary>
    public static IPipeConverter<ReadOnlyMemory<byte>> ByteBlocks(int chunkSize = 65536)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        return new BlockConverter(chunkSize);
    }

    /// <summary>Reads lines without terminators, preserving decoder state across reads.</summary>
    public static IPipeConverter<string> Lines(Encoding? encoding = null)
        => new LineConverter(encoding ?? new UTF8Encoding(false, true));

    private sealed class BlockConverter(int size) : IPipeConverter<ReadOnlyMemory<byte>>
    {
        public IAsyncEnumerable<ReadOnlyMemory<byte>> ConvertAsync(Stream source, CancellationToken cancellationToken = default)
            => source.Chunked(size, cancellationToken);
    }

    private sealed class ByteConverter : IPipeConverter<byte>
    {
        public async IAsyncEnumerable<byte> ConvertAsync(Stream source,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            var buffer = new byte[4096];
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                for (var i = 0; i < count; i++) yield return buffer[i];
        }
    }

    private sealed class LineConverter(Encoding encoding) : IPipeConverter<string>
    {
        public async IAsyncEnumerable<string> ConvertAsync(Stream source,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(source, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                yield return line;
        }
    }
}

/// <summary>Byte-specific convenience overloads, preserving the concrete message type.</summary>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public static class StreamInputPipeExtensions
{
    /// <summary>Starts reading individual bytes from a caller-owned stream.</summary>
    public static TSelf Pipe<TSelf>(this StreamInput<byte, TSelf> input, Stream source,
        CancellationToken cancellationToken = default) where TSelf : StreamInput<byte, TSelf>
        => input.Pipe(source, PipeConverters.Bytes, cancellationToken);

    /// <summary>Starts reading owned byte blocks from a caller-owned stream.</summary>
    public static TSelf Pipe<TSelf>(this StreamInput<ReadOnlyMemory<byte>, TSelf> input, Stream source,
        int chunkSize = 65536, CancellationToken cancellationToken = default)
        where TSelf : StreamInput<ReadOnlyMemory<byte>, TSelf>
        => input.Pipe(source, PipeConverters.ByteBlocks(chunkSize), cancellationToken);
}
