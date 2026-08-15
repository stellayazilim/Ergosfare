using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Plugins.Abstractions;

// This one line makes the assembly a plugin. The generator answers it by writing an IModule
// and an AddTiming() extension into this compilation — that extension is the whole install
// surface a consumer sees.
[assembly: ErgosfarePlugin("Timing")]

namespace Ergosfare.TimingPlugin;

/// <summary>
/// Reports how long each dispatch took.
/// </summary>
/// <remarks>
/// <para>
/// The whole plugin. Two hooks on the two points every pipeline has, and no state of its own:
/// the start timestamp travels between them through the execution context, which is the
/// pipeline's own per-dispatch channel. That is not a detail of this example — hooks are two
/// separate calls, so anything a service tried to remember between them would be shared by
/// every dispatch in flight.
/// </para>
/// <para>
/// Being stateless is also what makes the fixed singleton lifetime a non-question: there is
/// nothing in here for a second instance to hold differently.
/// </para>
/// </remarks>
public sealed class TimingHooks
{
    private const string StartedAt = "timing.startedAt";

    /// <summary>Before anything else in the pipeline runs.</summary>
    [PipelineInvokable(Hook.Start)]
    public void Started<TMessage>(TMessage message, ErgosfareContext context)
        => context.Set(StartedAt, Stopwatch.GetTimestamp());

    /// <summary>
    /// After the pipeline completed and before the result goes back to the call site.
    /// </summary>
    /// <remarks>
    /// <paramref name="logger"/> is not a hook parameter the generator knows about — anything
    /// it does not recognize is resolved from the dispatching provider at the call site, which
    /// is what lets a hook take a scoped dependency without the service capturing one.
    /// </remarks>
    [PipelineInvokable(Hook.Finish)]
    public void Finished<TMessage>(TMessage message, ErgosfareContext context, ILogger<TimingHooks> logger)
    {
        if (!context.TryGet<long>(StartedAt, out var startedAt))
        {
            return;
        }

        logger.LogInformation(
            "{Message} took {Elapsed:0.000} ms",
            typeof(TMessage).Name,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }
}
