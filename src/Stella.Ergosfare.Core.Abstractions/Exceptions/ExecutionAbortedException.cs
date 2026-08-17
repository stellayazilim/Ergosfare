
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Thrown by <see cref="ErgosfareContext.Abort()"/> when a participant ends the dispatch.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline stops where the signal was raised. Nothing downstream runs — not the rest
/// of the current stage, not the exception stage, not the final stage — and the pipeline
/// produces no result, which is why this exception carries none.
/// </para>
/// <para>
/// The signal travels out to the caller that asked for the dispatch, carrying whatever the
/// aborting participant attached: <see cref="Reason"/> to read, <see cref="Value"/> to act
/// on.
/// </para>
/// <code>
/// try
/// {
///     var id = await commands.SendAsync&lt;Guid&gt;(new PlaceOrder(...));
/// }
/// catch (ExecutionAbortedException aborted)
/// {
///     logger.LogInformation("order refused: {Reason}", aborted.Reason);
///     if (aborted.Value is ValidationFailure failure) { ... }
/// }
/// </code>
/// <para>
/// This is the exception-shaped channel and it behaves the same whether or not the pipeline
/// has interceptors. To carry outcomes as values instead, use the result-adapter surface.
/// </para>
/// </remarks>
public class ExecutionAbortedException : Exception
{
    private const string DefaultReason = "Execution was aborted";

    /// <summary>
    /// Initializes the signal with no stated reason.
    /// </summary>
    public ExecutionAbortedException() : base(DefaultReason)
    {
    }

    /// <summary>
    /// Initializes the signal with a reason.
    /// </summary>
    /// <param name="reason">
    /// Why the dispatch ended; becomes the exception message. <c>null</c> uses the default
    /// reason.
    /// </param>
    public ExecutionAbortedException(string? reason) : base(reason ?? DefaultReason)
    {
    }

    /// <summary>
    /// Initializes the signal with a reason and a value for the caller.
    /// </summary>
    /// <param name="reason">
    /// Why the dispatch ended; becomes the exception message. <c>null</c> uses the default
    /// reason.
    /// </param>
    /// <param name="value">
    /// Data about the abort for the caller to inspect. It is not the pipeline's result; an
    /// aborted pipeline has none.
    /// </param>
    public ExecutionAbortedException(string? reason, object? value) : base(reason ?? DefaultReason)
    {
        Value = value;
    }

    /// <summary>
    /// Why the dispatch ended, as the aborting participant stated it.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public string Reason => Message;

    /// <summary>
    /// What the aborting participant attached to the signal, or <c>null</c> if nothing.
    /// </summary>
    public object? Value { get; }
}
