namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>
/// Bridges between a chunk sequence and <see cref="Stream"/>, in both directions.
/// </summary>
/// <remarks>
/// Written against <see cref="IAsyncEnumerable{T}"/> rather than against
/// <see cref="ErgosfareStream{TChunk}"/>: a stream message is a sequence, so these apply to
/// it, and they apply equally to any other source. They are extensions rather than members
/// because they only make sense over bytes — a sequence of frames or of order lines has no
/// <see cref="Stream"/> to be, and its message should not carry a method that says otherwise.
/// </remarks>
public static class ByteStreamExtensions
{
    /// <summary>
    /// How much of a <see cref="Stream"/> is read into one chunk by default.
    /// </summary>
    public const int DefaultChunkSize = 64 * 1024;

    /// <summary>
    /// Presents a chunk sequence as a read-only <see cref="Stream"/>.
    /// </summary>
    /// <param name="source">The chunks to serve.</param>
    /// <returns>A forward-only stream over them.</returns>
    /// <remarks>
    /// For handing the payload to something that speaks <see cref="Stream"/> — a file, an
    /// image decoder, a serializer. Reading it consumes the sequence, so it inherits the
    /// single-pass contract of whatever it wraps.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public static Stream AsStream(this IAsyncEnumerable<ReadOnlyMemory<byte>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ChunkStream(source);
    }

    /// <summary>
    /// Reads a <see cref="Stream"/> as a chunk sequence.
    /// </summary>
    /// <param name="source">The stream to read.</param>
    /// <param name="chunkSize">How much to read into one chunk.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The chunks, in order, ending when the stream does.</returns>
    /// <remarks>
    /// The adopting direction: a request body or a file becomes the source a stream message
    /// hands to its handler, without the caller pumping anything.
    /// <para>
    /// Each chunk owns its bytes. Reading into one rented buffer and handing out slices of it
    /// would be cheaper by an allocation per chunk, and would make every chunk a window onto
    /// memory the next read overwrites — a handler that keeps one would find its contents
    /// changed underneath. A chunk is an item; what to do with it, including keeping it, is
    /// the handler's business.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is not positive.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunked(
        this Stream source,
        int chunkSize = DefaultChunkSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        while (true)
        {
            // A buffer per chunk, read into directly: one allocation, no copy, and the chunk
            // that comes out is nobody else's memory.
            var buffer = new byte[chunkSize];
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                yield break;
            }

            yield return buffer.AsMemory(0, read);
        }
    }

    /// <summary>
    /// A read-only <see cref="Stream"/> served from a chunk sequence.
    /// </summary>
    /// <param name="source">The chunks to serve.</param>
    private sealed class ChunkStream(IAsyncEnumerable<ReadOnlyMemory<byte>> source) : Stream
    {
        private IAsyncEnumerator<ReadOnlyMemory<byte>>? _enumerator;
        private ReadOnlyMemory<byte> _current;
        private bool _finished;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        /// <remarks>A sequence has no length until it ends.</remarks>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        /// <remarks>A forward-only stream has no position to set.</remarks>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_finished || buffer.IsEmpty)
            {
                return 0;
            }

            _enumerator ??= source.GetAsyncEnumerator(cancellationToken);

            while (_current.IsEmpty)
            {
                if (!await _enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    _finished = true;
                    return 0;
                }

                _current = _enumerator.Current;
            }

            var take = Math.Min(buffer.Length, _current.Length);
            _current.Span[..take].CopyTo(buffer.Span);
            _current = _current[take..];

            return take;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        /// <inheritdoc />
        /// <remarks>
        /// Synchronous reading would block a thread on a sequence that is produced
        /// asynchronously, which is the cost this whole surface exists to avoid.
        /// </remarks>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            if (_enumerator is not null)
            {
                await _enumerator.DisposeAsync().ConfigureAwait(false);
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
