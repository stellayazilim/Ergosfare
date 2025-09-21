using System;
using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions;


/// <summary>
/// Represents a snapshot stage of a pipeline execution step.
/// Useful for debugging, tracing, and recovery.
/// </summary>
public interface IPipelineCheckpoint
{
    /// <summary>
    /// Unique identifier of this checkpoint.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The input message at this stage.
    /// </summary>
    object Message { get; }

    /// <summary>
    /// The result produced at this stage, if any.
    /// </summary>
    object? Result { get; }

    /// <summary>
    /// Type of the handler that executed.
    /// </summary>
    Type HandlerType { get; }

    /// <summary>
    /// Parent checkpoint (if any).
    /// </summary>
    public IPipelineCheckpoint? Parent { get; }

    /// <summary>
    /// Child checkpoints executed within this scope.
    /// </summary>
    public List<IPipelineCheckpoint> Children { get; }

    /// <summary>
    /// Time when execution started.
    /// </summary>
    DateTimeOffset CapturedAt { get; }

}