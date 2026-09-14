
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Thrown when a dispatch has no compiled plan to run it. Nothing is dispatched at run time
/// that was not produced at compile time, so a pipeline the generator did not bake — or one
/// that no longer matches what it baked — fails loudly instead of running a degraded lane.
/// </summary>
/// <remarks>
/// The message states which construct went unplanned and what to do about it;
/// <see cref="Reason"/> says why without parsing. A message nobody serves still raises
/// <see cref="NoHandlerFoundException"/>, and a contested one still raises
/// <see cref="MultipleHandlerFoundException"/> — this exception covers the pipelines that
/// would have run, had a plan been compiled for them.
/// </remarks>
public class UnplannedDispatchException : InvalidOperationException
{
    /// <summary>
    /// Initializes the exception for one unplanned dispatch.
    /// </summary>
    /// <param name="messageType">The message type whose dispatch had no plan.</param>
    /// <param name="reason">Why the dispatch had no plan.</param>
    /// <param name="message">The exception message.</param>
    public UnplannedDispatchException(Type messageType, UnplannedDispatchReason reason, string message)
        : base(message)
    {
        MessageType = messageType;
        Reason = reason;
    }

    /// <summary>
    /// The message type whose dispatch had no plan.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Why the dispatch had no plan.
    /// </summary>
    public UnplannedDispatchReason Reason { get; }
}
