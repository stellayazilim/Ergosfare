using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Internal.Contexts;


/// <summary>
/// Provides snapshot functionality for pipeline execution.
/// </summary>
/// <remarks>
/// A snapshot represents a scoped result captured during handler execution,
/// independent of the overall pipeline result.  
/// <para>
/// Snapshots are recorded as <see cref="PipelineCheckpoint"/> instances nested under
/// the current handler checkpoint.  
/// </para>
/// <para>
/// If a snapshot with the given <paramref name="name"/> already exists in the
/// current context, the previously captured result is returned. Otherwise,
/// a new checkpoint is created with the provided <paramref name="snapshot"/> result.  
/// </para>
/// </remarks>
public class SnapshotService: ISnapshotService
{
    
    /// <summary>
    /// Captures or retrieves a snapshot within the current execution context.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the snapshot result.
    /// </typeparam>
    /// <param name="name">
    /// Unique name of the snapshot within the current handler scope.
    /// Used as the checkpoint identifier.
    /// </param>
    /// <param name="snapshot">
    /// The snapshot instance that produces the result to be captured.
    /// </param>
    /// <returns>
    /// A task containing the snapshot result.  
    /// Returns a previously captured value if one exists, otherwise
    /// evaluates and records the new snapshot result.
    /// </returns>
    public Task<TResult> Snapshot<TResult>(string name, ISnapshot<TResult> snapshot)
    {
        var ctx = (ErgosfareExecutionContext)AmbientExecutionContext.Current;
        var checkpoint = ctx.Checkpoints
            .LastOrDefault();
        var existing = checkpoint?.Children.Find(x => x.Id == name);
        if (existing is not null) 
            return Task.FromResult<TResult>((TResult)existing.Result!);
 
        // is not captured
        var childCheckpoint = new PipelineCheckpoint(
            name,
            ctx.Message,
            snapshot.Result,
            ctx.CurrentHandlerType!,
            checkpoint,
            []);
        childCheckpoint.Success = true;
        checkpoint?.Children.Add(childCheckpoint);
        return Task.FromResult(snapshot.Result);
    }
}