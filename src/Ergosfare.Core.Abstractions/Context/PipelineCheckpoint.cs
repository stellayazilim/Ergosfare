using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions;

    /// <summary>
    /// Represents a snapshot of a single pipeline execution step.
    /// Captures the message, result, handler descriptor, parent, child checkpoints, and timestamp.
    /// </summary>
public class PipelineCheckpoint : IPipelineCheckpoint
{

    public bool Success { get; set; }
    
    /// <summary>
    /// Represents root checkpoint id
    /// </summary>
    public const string Root = "root";
    
    /// <summary>
    /// Unique identifier of this checkpoint.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The message being processed at this checkpoint.
    /// </summary>
    public object Message { get; }

    /// <summary>
    /// The result of processing at this checkpoint, if any.
    /// </summary>
    public object? Result { get; }

    /// <summary>
    /// The type of the handler that executed this checkpoint.
    /// </summary>
    public Type HandlerType { get; } 

    /// <summary>
    /// The parent checkpoint, or null if this is the root checkpoint.
    /// </summary>
    public IPipelineCheckpoint? Parent { get; }

    /// <summary>
    /// The child checkpoints executed within this checkpoint.
    /// </summary>
    public List<IPipelineCheckpoint> Children { get; }

    /// <summary>
    /// The timestamp when this checkpoint was captured.
    /// Automatically set when the checkpoint is created.
    /// </summary>
    public DateTimeOffset CapturedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineCheckpoint"/> class with a specific identifier.
    /// </summary>
    /// <param name="id">Unique checkpoint identifier.</param>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The result of processing, if any.</param>
    /// <param name="handlerType">The handler type that executed this checkpoint.</param>
    /// <param name="parent">The parent checkpoint, or null if this is the root.</param>
    /// <param name="subCheckpoints">Child checkpoints executed within this checkpoint.</param>
    public PipelineCheckpoint(
        string id,
        object message,
        object? result,
        Type handlerType,
        IPipelineCheckpoint? parent,
        IEnumerable<IPipelineCheckpoint> subCheckpoints
    )
    {
        Id = id;
        Message = message;
        Result = result;
        HandlerType = handlerType;
        Parent = parent;
        Children = subCheckpoints.ToList();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineCheckpoint"/> class with an auto-generated identifier.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The result of processing, if any.</param>
    /// <param name="handlerType">The handler type that executed this checkpoint.</param>
    /// <param name="parent">The parent checkpoint, or null if this is the root.</param>
    /// <param name="subCheckpoints">Child checkpoints executed within this checkpoint.</param>
    public PipelineCheckpoint(
        object message,
        object? result,
        Type handlerType,
        IPipelineCheckpoint? parent,
        IEnumerable<IPipelineCheckpoint> subCheckpoints)
    {
        Id = Guid.NewGuid().ToString();
        Message = message;
        Result = result;
        HandlerType = handlerType;
        Parent = parent;
        Children = subCheckpoints.ToList();
    }
}