
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
///     Raised by <see cref="ErgosfareContext.Abort()" />: a participant stopped the pipeline.
/// </summary>
/// <remarks>
///     <para>
///         This is Ergosfare's own signal and it means exactly what it says — the pipeline
///         is cut where it stands. Nothing further runs: not the remaining participants of
///         the current stage, not the exception stage, not the final stage. There is no
///         pipeline result to expect either; a stopped pipeline did not produce one.
///     </para>
///     <para>
///         It reaches the caller, because the caller is who asked for the work. Whatever the
///         aborting participant chose to say travels with it — <see cref="Reason" /> for a
///         human, <see cref="Value" /> for a program:
///     </para>
///     <code>
///     try
///     {
///         var id = await commands.SendAsync&lt;Guid&gt;(new PlaceOrder(...));
///     }
///     catch (ExecutionAbortedException aborted)
///     {
///         logger.LogInformation("order refused: {Reason}", aborted.Reason);
///         if (aborted.Value is ValidationFailure failure) { ... }
///     }
///     </code>
///     <para>
///         The mechanism does not change with the shape of the pipeline: with interceptors
///         or without, the signal travels straight out. Applications that would rather carry
///         outcomes as values than as exceptions have the result-adapter surface for that;
///         abort is the exception-shaped channel, and it is deliberately the loud one.
///     </para>
/// </remarks>
public class ExecutionAbortedException : Exception
{
    private const string DefaultReason = "Execution was aborted";

    /// <summary>Stops a pipeline, carrying nothing but the signal itself.</summary>
    public ExecutionAbortedException() : base(DefaultReason)
    {
    }

    /// <summary>Stops a pipeline, saying why.</summary>
    /// <param name="reason">Why the pipeline was stopped; also the exception message.</param>
    public ExecutionAbortedException(string? reason) : base(reason ?? DefaultReason)
    {
    }

    /// <summary>Stops a pipeline, saying why and handing the caller something to act on.</summary>
    /// <param name="reason">Why the pipeline was stopped; also the exception message.</param>
    /// <param name="value">
    ///     Data about the abort for the caller to inspect. It is not the pipeline's result;
    ///     a stopped pipeline has none.
    /// </param>
    public ExecutionAbortedException(string? reason, object? value) : base(reason ?? DefaultReason)
    {
        Value = value;
    }

    /// <summary>Why the pipeline was stopped, as the aborting participant stated it.</summary>
    // ReSharper disable once UnusedMember.Global
    public string Reason => Message;

    /// <summary>What the aborting participant attached to the signal, if anything.</summary>
    public object? Value { get; }
}
