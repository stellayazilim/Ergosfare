using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Harness;

/// <summary>
/// Hands a <see cref="PipelineRecorder"/> to a dispatch through the mediation settings'
/// <c>Items</c> dictionary and reads the stage marks back off the caller's own object —
/// the whole observation channel, built from public surface only.
/// </summary>
public static class Recording
{
    /// <summary>A context carrying <paramref name="recorder"/> into a command dispatch.</summary>
    /// <remarks>
    /// Built directly rather than rented: a context the caller constructs is never pooled,
    /// which is exactly what a test wants when it means to read back what the pipeline wrote.
    /// </remarks>
    public static ErgosfareContext Commands(this PipelineRecorder recorder)
        => new(new Dictionary<object, object?> { [PipelineRecorder.ItemsKey] = recorder });

    /// <summary>A context carrying <paramref name="recorder"/> into a query dispatch.</summary>
    /// <remarks>
    /// Built directly rather than rented: a context the caller constructs is never pooled,
    /// which is exactly what a test wants when it means to read back what the pipeline wrote.
    /// </remarks>
    public static ErgosfareContext Queries(this PipelineRecorder recorder)
        => new(new Dictionary<object, object?> { [PipelineRecorder.ItemsKey] = recorder });

    /// <summary>A context carrying <paramref name="recorder"/> into a publish.</summary>
    /// <remarks>
    /// Built directly rather than rented: a context the caller constructs is never pooled,
    /// which is exactly what a test wants when it means to read back what the pipeline wrote.
    /// </remarks>
    public static ErgosfareContext Events(this PipelineRecorder recorder)
        => new(new Dictionary<object, object?> { [PipelineRecorder.ItemsKey] = recorder });

    /// <summary>Shorthand for a pipeline participant marking its own stage.</summary>
    public static void Mark(this ErgosfareContext context, string stage, string? detail = null)
        => PipelineRecorder.From(context).Mark(stage, detail);
}
