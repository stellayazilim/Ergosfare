using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Abstractions.Streaming;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed record UploadMeta(string FileName, long DeclaredLength);

public sealed record UploadReport(long Bytes, int Chunks, string FileName);

public sealed class FileUpload : ErgosfareCommandStream<byte[], UploadMeta, UploadReport>
{
    public FileUpload(UploadMeta meta) : base(meta) { }

    public FileUpload(UploadMeta meta, IAsyncEnumerable<byte[]> source) : base(meta, source) { }
}

public sealed class FileUploadHandler : ICommandHandler<FileUpload, UploadReport>
{
    public async ValueTask<UploadReport> HandleAsync(FileUpload command, ErgosfareContext context)
    {
        long bytes = 0;
        var chunks = 0;

        await foreach (var chunk in command.WithCancellation(context.CancellationToken))
        {
            bytes += chunk.Length;
            chunks++;
        }

        return new UploadReport(bytes, chunks, command.Meta.FileName);
    }
}

/// <summary>
/// The guarded twin of <see cref="FileUpload"/>: the pre-stage interceptor below is part
/// of this message's compiled pipeline, so the unguarded upload tests keep their own
/// message type — a compiled plan bakes the full discoverable pipeline, and one message
/// cannot be dispatched both with and without the guard.
/// </summary>
public sealed class GuardedFileUpload : ErgosfareCommandStream<byte[], UploadMeta, UploadReport>
{
    public GuardedFileUpload(UploadMeta meta) : base(meta) { }

    public GuardedFileUpload(UploadMeta meta, IAsyncEnumerable<byte[]> source) : base(meta, source) { }
}

public sealed class GuardedFileUploadHandler : ICommandHandler<GuardedFileUpload, UploadReport>
{
    public async ValueTask<UploadReport> HandleAsync(GuardedFileUpload command, ErgosfareContext context)
    {
        long bytes = 0;
        var chunks = 0;

        await foreach (var chunk in command.WithCancellation(context.CancellationToken))
        {
            bytes += chunk.Length;
            chunks++;
        }

        return new UploadReport(bytes, chunks, command.Meta.FileName);
    }
}

public sealed class TooLarge(string message) : Exception(message);

/// <summary>
/// The stage that runs before the payload moves. It reads what is known up front and
/// nothing else, which is what lets it refuse an upload without a byte of it arriving.
/// </summary>
public sealed class RejectOversizedUploads : ICommandPreInterceptor<GuardedFileUpload>
{
    public ValueTask<GuardedFileUpload> HandleAsync(GuardedFileUpload command, ErgosfareContext context)
        => command.Meta.DeclaredLength > 1_000
            ? throw new TooLarge($"'{command.Meta.FileName}' declares {command.Meta.DeclaredLength} bytes")
            : ValueTask.FromResult(command);
}

public sealed record CopyMeta(string FileName);

public sealed record CopyReport(long Bytes);

/// <summary>
/// The byte shape: chunks are memory segments, which is what the bridge to
/// <see cref="Stream"/> is written against.
/// </summary>
public sealed class FileCopy : ErgosfareCommandStream<ReadOnlyMemory<byte>, CopyMeta, CopyReport>
{
    public FileCopy(CopyMeta meta, IAsyncEnumerable<ReadOnlyMemory<byte>> source) : base(meta, source) { }
}

public sealed class FileCopyHandler : ICommandHandler<FileCopy, CopyReport>
{
    public async ValueTask<CopyReport> HandleAsync(FileCopy command, ErgosfareContext context)
    {
        // The handler wants a Stream, and the message is a sequence: the bridge is one
        // call, and nothing is buffered whole on the way.
        await using var payload = command.AsStream();
        using var destination = new MemoryStream();

        await payload.CopyToAsync(destination, context.CancellationToken);

        return new CopyReport(destination.Length);
    }
}

/// <summary>
/// A message whose payload arrives in chunks, dispatched through the ordinary command
/// surface: no streaming verb, no streaming handler contract, no lane of its own. What makes
/// the dispatch streaming is the message's type, and everything the pipeline does around it
/// — selection, interceptor stages, the compiled plan — is what it does for any command.
/// </summary>
public class StreamMessageTests
{
    private static ServiceProvider BuildProvider()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<FileUploadHandler>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AWrittenStream_ReachesTheHandlerChunkByChunkAndAnswersWithOneResult()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var upload = new FileUpload(new UploadMeta("clip.mp4", 5));

        // The dispatch is started and deliberately not awaited: the handler is already
        // pulling, and awaiting here would wait for chunks this method has not written yet.
        var call = mediator.SendAsync(upload);

        await upload.WriteAsync([1, 2, 3]);
        await upload.WriteAsync([4, 5]);
        upload.Complete();

        var report = await call;

        Assert.Equal(5, report.Bytes);
        Assert.Equal(2, report.Chunks);
        Assert.Equal("clip.mp4", report.FileName);
        Assert.Equal(2, upload.Info.Chunks);
        Assert.Equal(StreamCompletion.Completed, upload.Info.Completion);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnAdoptedSource_NeedsNoPumpingAndSoNoDeferredAwait()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The adopting form: the source already exists and feeds itself, which is the shape
        // a request body has. Nothing is pumped, so the ordinary await is correct here.
        var report = await mediator.SendAsync(new FileUpload(new UploadMeta("body.mp4", 6), Source()));

        Assert.Equal(6, report.Bytes);
        Assert.Equal(3, report.Chunks);

        static async IAsyncEnumerable<byte[]> Source()
        {
            for (var i = 0; i < 3; i++)
            {
                await Task.Yield();
                yield return [1, 2];
            }
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TheBoundedBuffer_MakesTheWriterWaitForTheHandler()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // One slot: the second write cannot complete until the handler has taken the first,
        // which is the back-pressure that keeps a payload from existing as one value.
        var upload = new FileUpload(new UploadMeta("slow.mp4", 2)) { };
        var call = mediator.SendAsync(upload);

        await upload.WriteAsync([1]);
        await upload.WriteAsync([2]);
        upload.Complete();

        var report = await call;

        Assert.Equal(2, report.Bytes);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TheChunks_CanOnlyBeTakenOnce()
    {
        var upload = new FileUpload(new UploadMeta("once.mp4", 0));
        upload.Complete();

        await foreach (var _ in upload)
        {
            // Draining the first pass is what claims the reader.
        }

        var second = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in upload)
            {
            }
        });

        Assert.Contains("single-pass", second.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AnAdoptedStream_RefusesTheWritingSide()
    {
        var upload = new FileUpload(new UploadMeta("body.mp4", 0), Empty());

        Assert.True(upload.IsAdopted);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await upload.WriteAsync([1]));
        Assert.Throws<InvalidOperationException>(() => upload.Complete());

        static async IAsyncEnumerable<byte[]> Empty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AFaultedProducer_SurfacesAtTheHandlersRead()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var upload = new FileUpload(new UploadMeta("broken.mp4", 0));
        var call = mediator.SendAsync(upload);

        await upload.WriteAsync([1]);
        upload.Fault(new InvalidOperationException("source broke"));

        // The failure is the producer's, and it reaches the caller through the dispatch —
        // the handler's read is where it surfaces.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await call);

        Assert.Equal("source broke", thrown.Message);
        Assert.Equal(StreamCompletion.Faulted, upload.Info.Completion);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AByteStream_BridgesToAndFromSystemIOStream()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<FileCopyHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // In: a Stream becomes the source the message adopts, the way a request body would.
        using var body = new MemoryStream([1, 2, 3, 4, 5, 6, 7]);

        var report = await mediator.SendAsync(new FileCopy(new CopyMeta("copy.bin"), body.Chunked(chunkSize: 3)));

        // Out: the handler read it back as a Stream, three chunks becoming seven bytes.
        Assert.Equal(7, report.Bytes);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AChunkIsAnItem_AndKeepingItKeepsWhatItWas()
    {
        using var body = new MemoryStream([1, 2, 3, 4, 5, 6]);

        var kept = new List<ReadOnlyMemory<byte>>();

        await foreach (var chunk in body.Chunked(chunkSize: 2))
        {
            kept.Add(chunk);
        }

        // Handing out slices of one reused buffer would leave all three showing the last
        // read. A chunk owns its bytes, so collecting them is ordinary code.
        Assert.Equal(3, kept.Count);
        Assert.Equal(new byte[] { 1, 2 }, kept[0].ToArray());
        Assert.Equal(new byte[] { 3, 4 }, kept[1].ToArray());
        Assert.Equal(new byte[] { 5, 6 }, kept[2].ToArray());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ThePreStage_RefusesTheUploadBeforeAChunkMoves()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<GuardedFileUploadHandler>()
                .Register<RejectOversizedUploads>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var upload = new GuardedFileUpload(new UploadMeta("huge.mp4", 4_000_000_000));

        var thrown = await Assert.ThrowsAsync<TooLarge>(async () => await mediator.SendAsync(upload));

        Assert.Contains("4000000000", thrown.Message);

        // The whole point: the metadata was enough to decide, so the payload never started.
        Assert.Equal(0, upload.Info.Chunks);

        // And the pipeline that stopped ended the stream with it, so a producer still
        // writing finds out rather than waiting on a reader that is not coming.
        Assert.Equal(StreamCompletion.Faulted, upload.Info.Completion);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ARefusedDispatch_EndsTheStreamInsteadOfLeavingTheWriterWaiting()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<GuardedFileUploadHandler>()
                .Register<RejectOversizedUploads>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var upload = new GuardedFileUpload(new UploadMeta("huge.mp4", 4_000_000_000));
        var call = mediator.SendAsync(upload);

        await Assert.ThrowsAsync<TooLarge>(async () => await call);

        // Without the pipeline ending the stream, this producer would fill the buffer and
        // then wait on a reader the refusal took away. It finds out on the next write
        // instead, and the message says what to do about it.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await upload.WriteAsync([1]).AsTask().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Contains("Await the dispatch to see why", thrown.Message);
        Assert.Equal(StreamCompletion.Faulted, upload.Info.Completion);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ASucceedingDispatch_LeavesTheStreamAsTheProducerCompletedIt()
    {
        await using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var upload = new FileUpload(new UploadMeta("fine.mp4", 2));
        var call = mediator.SendAsync(upload);

        await upload.WriteAsync([1, 2]);
        upload.Complete();

        await call;

        // Ending a stream the producer already ended changes nothing: the first answer wins.
        Assert.Equal(StreamCompletion.Completed, upload.Info.Completion);
    }
}
