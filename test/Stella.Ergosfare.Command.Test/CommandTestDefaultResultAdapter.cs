// The default-adapter surface is experimental, and configuring one is what these fixtures
// exist to exercise.
#pragma warning disable ERGOEXP001

using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The one fallback adapter this assembly configures.
/// </summary>
/// <remarks>
/// A compilation names a single default adapter — two would leave no way to say which one a
/// pipeline was compiled against, which is ERGO020 — so the carriers of every fixture that
/// needs a configured fallback are served from here, one
/// <see cref="IResultAdapter{TResult}"/> implementation each.
/// </remarks>
public sealed class CommandTestDefaultResultAdapter :
    IResultAdapter<DefaultBoundOutcome>,
    IResultAdapter<StagedPlanExecutionTests.DefaultGateOutcome>
{
    /// <inheritdoc />
    public bool TryGetException(in DefaultBoundOutcome result, out Exception? exception)
    {
        exception = result.Error;
        return exception is not null;
    }

    /// <inheritdoc />
    public bool TryGetException(in StagedPlanExecutionTests.DefaultGateOutcome result, out Exception? exception)
    {
        exception = result.Error;
        return exception is not null;
    }
}
