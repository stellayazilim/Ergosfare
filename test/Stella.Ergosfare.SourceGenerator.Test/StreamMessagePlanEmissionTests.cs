using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// A message whose payload arrives in chunks is a message like any other as far as the
/// generator is concerned: it is classified through the contract its base declares, it gets
/// its dispatch roots, and — this is the point — it gets the same compiled plan an ordinary
/// command with the same participants would get. Streaming needs no plan family of its own
/// because it is not a lane of its own.
/// </summary>
public class StreamMessagePlanEmissionTests
{
    private const string StreamApp = """
        #pragma warning disable ERGOEXP003
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Commands.Abstractions.Streaming;
        using Stella.Ergosfare.Core.Abstractions;

        namespace TestApp
        {
            public sealed record UploadMeta(string FileName);

            public sealed record UploadReport(long Bytes);

            public sealed class FileUpload : ErgosfareCommandStream<byte[], UploadMeta, UploadReport>
            {
                public FileUpload(UploadMeta meta) : base(meta) { }
            }

            public sealed class FileUploadHandler : ICommandHandler<FileUpload, UploadReport>
            {
                public async ValueTask<UploadReport> HandleAsync(FileUpload command, ErgosfareContext context)
                {
                    long bytes = 0;

                    await foreach (var chunk in command.WithCancellation(context.CancellationToken))
                    {
                        bytes += chunk.Length;
                    }

                    return new UploadReport(bytes);
                }
            }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void AStreamMessage_IsClassifiedThroughItsBaseAndGetsItsDispatchRoots()
    {
        var result = GeneratorTestHost.Run(StreamApp + """
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The command contract is reached through the streaming base, two levels up — the
        // message declares nothing itself.
        Assert.Contains("AddMessage<global::TestApp.FileUpload>();", result.GeneratedSource);
        Assert.Contains("AddResult<global::TestApp.FileUpload, global::TestApp.UploadReport>();", result.GeneratedSource);

        // And nothing streaming-shaped is emitted: no lane, no stream root, no second table.
        Assert.DoesNotContain("AddStream<global::TestApp.FileUpload", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AStreamMessageWithInterceptors_GetsTheSameCompiledPlanAnOrdinaryCommandWould()
    {
        var result = GeneratorTestHost.Run(StreamApp + """

            public sealed class RejectEmptyNames : ICommandPreInterceptor<FileUpload>
            {
                public ValueTask<FileUpload> HandleAsync(FileUpload command, ErgosfareContext context)
                    => ValueTask.FromResult(command);
            }

            public sealed class StampReport : ICommandPostInterceptor<FileUpload, UploadReport>
            {
                public ValueTask<UploadReport> HandleAsync(FileUpload command, UploadReport messageResult, ErgosfareContext context)
                    => ValueTask.FromResult(messageResult);
            }
        }
        """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The claim under test: the pipeline is compiled, exactly as it would be for a
        // command whose payload arrives in one piece.
        Assert.Contains(
            "AddStagedPlan<global::TestApp.FileUpload, global::TestApp.UploadReport>",
            result.GeneratedSource);
        Assert.Contains("global::TestApp.RejectEmptyNames", result.GeneratedSource);
        Assert.Contains("global::TestApp.StampReport", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PublishingAStreamMessage_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run("""
            #pragma warning disable ERGOEXP003
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Abstractions.Streaming;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public sealed record UploadMeta(string FileName);

                public sealed class FileUpload : ErgosfareCommandStream<byte[], UploadMeta>
                {
                    public FileUpload(UploadMeta meta) : base(meta) { }
                }

                public sealed class FileUploadHandler : ICommandHandler<FileUpload>
                {
                    public ValueTask HandleAsync(FileUpload command, ErgosfareContext context) => default;
                }

                public static class Caller
                {
                    public static async Task Run(IEventMediator mediator, FileUpload upload)
                        => await mediator.PublishAsync(upload);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // One channel, many subscribers: whoever reads first takes the payload. There is no
        // arrangement that makes it work, so the call is the error.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGO022");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("FileUpload", diagnostic.GetMessage());
    }
}
