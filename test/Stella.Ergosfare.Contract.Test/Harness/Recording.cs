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
    /// <summary>Contextual items that carry <paramref name="recorder"/> into a command dispatch.</summary>
    public static Dictionary<object, object?> Commands(this PipelineRecorder recorder)
        => new() { [PipelineRecorder.ItemsKey] = recorder };

    /// <summary>Contextual items that carry <paramref name="recorder"/> into a query dispatch.</summary>
    public static Dictionary<object, object?> Queries(this PipelineRecorder recorder)
        => new() { [PipelineRecorder.ItemsKey] = recorder };

    /// <summary>Contextual items that carry <paramref name="recorder"/> into a publish.</summary>
    public static Dictionary<object, object?> Events(this PipelineRecorder recorder)
        => new() { [PipelineRecorder.ItemsKey] = recorder };

    /// <summary>Shorthand for a pipeline participant marking its own stage.</summary>
    public static void Mark(this ErgosfareContext context, string stage, string? detail = null)
        => PipelineRecorder.From(context).Mark(stage, detail);
}
