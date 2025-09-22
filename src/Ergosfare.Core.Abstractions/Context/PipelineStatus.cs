namespace Ergosfare.Core.Abstractions;

/// <summary>
/// Represents the execution state of a pipeline checkpoint.
/// </summary>
public enum PipelineStatus
{
    /// <summary>
    /// The pipeline has just started execution.
    /// </summary>
    Begin,

    /// <summary>
    /// The pipeline execution has started from a previously saved snapshot.
    /// </summary>
    BeginFromSnapshot,
    
    /// <summary>
    /// The checkpoint has successfully completed execution.
    /// </summary>
    Completed,

    /// <summary>
    /// The checkpoint failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// The checkpoint execution has been paused and may be resumed.
    /// </summary>
    Paused
}