using System.Diagnostics;
using System.Text;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Harness;

/// <summary>
/// The suite's single observation instrument: every pipeline participant marks its own
/// entry through it, so a scenario's expectation is one ordered list of stage names.
/// </summary>
/// <remarks>
/// The recorder travels on the mediation settings' <c>Items</c> dictionary — the public
/// way to hand data to a dispatch — and is read back off the caller's own settings object
/// when the dispatch returns. Nothing here reaches below the public surface.
/// <para>
/// When <see cref="ContractDiagnostics.CaptureStacks"/> is on, each mark also captures the
/// managed frames between the mediator entry point and the marking stage, and
/// <see cref="Dump"/> renders them as a nested per-stage trace. That capture is diagnostic
/// only: no test asserts on it, and it is off unless the environment asks for it, so
/// stage ordering stays the only pinned observation.
/// </para>
/// </remarks>
public sealed class PipelineRecorder
{
    /// <summary>The mediation-settings item key the recorder travels under.</summary>
    public const string ItemsKey = "ergosfare.contract.recorder";

    /// <summary>
    /// Names the scenario in the dump. Shared scenario code runs under more than one
    /// registration axis, and the captured frames alone cannot tell them apart.
    /// </summary>
    public string? Label { get; init; }

    private static readonly PipelineRecorder Silent = new();

    private readonly List<StageMark> _marks = [];
    private readonly Lock _gate = new();

    /// <summary>The stage names in the order they ran — what scenarios assert on.</summary>
    public IReadOnlyList<string> Stages
    {
        get
        {
            lock (_gate)
            {
                return _marks.ConvertAll(static m => m.Stage);
            }
        }
    }

    /// <summary>The details recorded alongside a stage, in order.</summary>
    public IReadOnlyList<string?> Details
    {
        get
        {
            lock (_gate)
            {
                return _marks.ConvertAll(static m => m.Detail);
            }
        }
    }

    /// <summary>The detail recorded by the first mark of the named stage, or <c>null</c>.</summary>
    public string? DetailOf(string stage)
    {
        lock (_gate)
        {
            return _marks.Find(m => m.Stage == stage).Detail;
        }
    }

    /// <summary>The position of the named stage in the run, or <c>-1</c> when it never ran.</summary>
    public int IndexOf(string stage)
    {
        lock (_gate)
        {
            return _marks.FindIndex(m => m.Stage == stage);
        }
    }

    /// <summary>Records that <paramref name="stage"/> ran, optionally with what it saw.</summary>
    public void Mark(string stage, string? detail = null)
    {
        var frames = ContractDiagnostics.CaptureStacks ? CaptureFrames() : null;

        lock (_gate)
        {
            _marks.Add(new StageMark(stage, detail, frames));
        }
    }

    /// <summary>
    /// The recorder carried by the dispatch, or a shared silent one when the caller passed
    /// no settings — so a participant can mark unconditionally.
    /// </summary>
    public static PipelineRecorder From(ErgosfareContext context)
        // TryGet annotates its out parameter non-null, but it casts whatever was stored and
        // yields default! on a miss — a null recorder reaches here despite the annotation.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        => context.TryGet<PipelineRecorder>(ItemsKey, out var recorder) && recorder is not null
            ? recorder
            : Silent;

    /// <summary>A nested, human-readable rendering of everything the dispatch did.</summary>
    public string Dump(string? title = null)
    {
        var builder = new StringBuilder();

        if (title is not null)
        {
            builder.AppendLine(title);
        }

        lock (_gate)
        {
            for (var i = 0; i < _marks.Count; i++)
            {
                var mark = _marks[i];
                builder.Append("  ").Append(i + 1).Append(". ").Append(mark.Stage);

                if (mark.Detail is not null)
                {
                    builder.Append("  <- ").Append(mark.Detail);
                }

                builder.AppendLine();

                if (mark.Frames is null)
                {
                    continue;
                }

                for (var depth = 0; depth < mark.Frames.Length; depth++)
                {
                    builder.Append("     ").Append(new string(' ', depth * 2))
                           .Append("| ").AppendLine(mark.Frames[depth]);
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Asserts the recorded stages, failing with the full nested dump attached — the
    /// ordering failures this suite exists to catch are unreadable without it.
    /// </summary>
    public void AssertStages(params string[] expected)
    {
        var actual = Stages;

        if (actual.Count == expected.Length)
        {
            var equal = true;

            for (var i = 0; i < expected.Length; i++)
            {
                if (!string.Equals(actual[i], expected[i], StringComparison.Ordinal))
                {
                    equal = false;
                    break;
                }
            }

            if (equal)
            {
                ContractDiagnostics.Publish(this, expected);
                return;
            }
        }

        Assert.Fail(
            $"""
             Pipeline stages did not match.
               expected: [{string.Join(", ", expected)}]
               actual:   [{string.Join(", ", actual)}]
             {Dump("dispatch trace:")}
             """);
    }

    private static string[] CaptureFrames()
    {
        var trace = new StackTrace(skipFrames: 2, fNeedFileInfo: false);
        var frames = new List<string>();

        foreach (var frame in trace.GetFrames())
        {
            var method = frame.GetMethod();

            if (method?.DeclaringType is null)
            {
                continue;
            }

            var declaring = method.DeclaringType.FullName ?? method.DeclaringType.Name;

            // The dispatch-relevant window: everything from the mediator facade down to
            // the marking stage. Test-runner and async-machinery frames add noise only.
            if (declaring.StartsWith("Xunit", StringComparison.Ordinal)
                || declaring.StartsWith("System.Reflection", StringComparison.Ordinal)
                || declaring.StartsWith("System.RuntimeMethodHandle", StringComparison.Ordinal)
                || declaring.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal)
                || declaring.StartsWith("System.Threading", StringComparison.Ordinal))
            {
                continue;
            }

            frames.Add($"{declaring}.{method.Name}");
        }

        frames.Reverse();
        return frames.ToArray();
    }

    private readonly record struct StageMark(string Stage, string? Detail, string[]? Frames);
}
