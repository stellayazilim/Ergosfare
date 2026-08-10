using System.Collections.Concurrent;
using System.Text;

namespace Stella.Ergosfare.Contract.Test.Harness;

/// <summary>
/// Opt-in diagnostics for the modernization work: with
/// <c>ERGOSFARE_CONTRACT_STACKDUMP=1</c> in the environment, every passing scenario
/// contributes its nested pipeline trace — which stage ran when, and the managed frames
/// underneath it — to one dump file, so a redesigned core can be diffed against the
/// current one frame by frame.
/// </summary>
/// <remarks>
/// Off by default and never asserted on: capturing stacks changes nothing the tests
/// observe, and the suite's contract stays the stage ordering alone. The dump lands in
/// <c>ERGOSFARE_CONTRACT_DUMP_PATH</c> when set, otherwise next to the test assembly as
/// <c>contract-stackdump.txt</c>, and is flushed at process exit with its sections in a
/// stable order — two dumps of an unchanged core diff empty.
/// </remarks>
public static class ContractDiagnostics
{
    private static readonly ConcurrentQueue<string> Sections = new();

    /// <summary>Whether pipeline marks capture their managed frames.</summary>
    public static readonly bool CaptureStacks =
        Environment.GetEnvironmentVariable("ERGOSFARE_CONTRACT_STACKDUMP") is "1" or "true";

    static ContractDiagnostics()
    {
        if (CaptureStacks)
        {
            AppDomain.CurrentDomain.ProcessExit += static (_, _) => Flush();
        }
    }

    /// <summary>Contributes a scenario's trace to the dump. No-op unless capture is on.</summary>
    public static void Publish(PipelineRecorder recorder, IReadOnlyList<string> expected)
    {
        if (!CaptureStacks)
        {
            return;
        }

        var scenario = recorder.Label is null ? string.Empty : $"{recorder.Label} — ";
        Sections.Enqueue(recorder.Dump($"{scenario}stages: [{string.Join(", ", expected)}]"));
    }

    private static void Flush()
    {
        if (Sections.IsEmpty)
        {
            return;
        }

        var path = Environment.GetEnvironmentVariable("ERGOSFARE_CONTRACT_DUMP_PATH")
                   ?? Path.Combine(AppContext.BaseDirectory, "contract-stackdump.txt");

        var sections = new List<string>();

        while (Sections.TryDequeue(out var section))
        {
            sections.Add(section);
        }

        // Scenarios finish in whatever order xunit's parallelism gives them, so the queue
        // order changes from run to run. The dump is meant to be diffed against a stored
        // baseline, and only a stable order makes that diff mean anything.
        sections.Sort(StringComparer.Ordinal);

        var builder = new StringBuilder();

        foreach (var section in sections)
        {
            builder.AppendLine(section);
        }

        File.WriteAllText(path, builder.ToString());
    }
}
