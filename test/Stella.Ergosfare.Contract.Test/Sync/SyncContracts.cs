using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Contract.Test.Sync;

/// <summary>
/// The synchronous participants' shared vocabulary and behavior. Each registration axis
/// closes these bases over its own concrete message types, so the axes run byte-identical
/// participant code and any difference the scenarios see is a difference in registration.
/// </summary>
/// <remarks>
/// Everything here is excluded from discovery: these are shapes, not registrable
/// constructs. Exclusion is not inherited, so the closing types stay discoverable.
/// <para>
/// Every interceptor base also implements the module marker (<see cref="ICommand"/>). That
/// is not decoration: <c>CommandModuleBuilder</c> rejects any type that is not assignable
/// to <see cref="ICommand"/>, and the synchronous contracts in
/// <c>Stella.Ergosfare.Core.Abstractions.Handlers</c> have no module-flavored facade to
/// inherit it from — unlike <c>ICommandPreInterceptor&lt;T&gt;</c> and friends, which
/// extend <see cref="ICommand"/> themselves. A bare <c>IPreInterceptor&lt;T&gt;</c> cannot
/// be registered at all; see the suite README.
/// </para>
/// </remarks>
public static class SyncVocabulary
{
    /// <summary>Payload marker that makes a handler throw.</summary>
    public const string Throw = "throw";

    /// <summary>Payload marker that makes a pre-interceptor abort.</summary>
    public const string Abort = "abort";

    /// <summary>Suffix a synchronous pre-interceptor appends when it rewrites the message.</summary>
    public const string Rewritten = "+sync-pre";

    /// <summary>Suffix a synchronous post-interceptor appends to a string result.</summary>
    public const string Posted = "+sync-post";

    /// <summary>What a synchronous string exception interceptor falls back to.</summary>
    public const string StringFallback = "sync-fallback";

    /// <inheritdoc cref="Pipeline.PipelineVocabulary.Describe(object?)"/>
    public static string Describe(object? result) => result switch
    {
        null => "null",
        string text => text,
        Unit unit => ReferenceEquals(unit, Unit.Value) ? nameof(Unit) : "unit:other",
        ValueTask => nameof(ValueTask),
        _ => result.ToString() ?? result.GetType().Name,
    };

    /// <summary>Renders a stage's exception argument for the recorder.</summary>
    public static string Describe(Exception? exception)
        => exception is null ? "none" : exception.GetType().Name;
}

/// <summary>The exception a scenario's handler throws, distinguishable from framework ones.</summary>
public sealed class SyncFailure(string message) : Exception(message);

/// <summary>A void command carrying the payload that drives its scenario.</summary>
[ExcludeFromDiscovery]
public interface ISyncPayloadCommand : ICommand
{
    /// <summary>Drives handler/interceptor behavior and records what each stage saw.</summary>
    string Payload { get; set; }
}

/// <summary>A string-result command carrying the payload that drives its scenario.</summary>
[ExcludeFromDiscovery]
public interface ISyncPayloadResultCommand : ICommand<string>
{
    /// <inheritdoc cref="ISyncPayloadCommand.Payload"/>
    string Payload { get; set; }
}

// ---------------------------------------------------------------------------
// void pipeline — asynchronous handler, synchronous interceptors
// ---------------------------------------------------------------------------

/// <summary>
/// The main handler stays asynchronous on purpose: a synchronous main handler disqualifies
/// its message from every compile-time plan, and these scenarios exist to run the emitted
/// synchronous interceptor calls. <see cref="SyncMainHandlerContract"/> covers the
/// synchronous main-handler contracts themselves.
/// </summary>
[ExcludeFromDiscovery]
public abstract class SyncVoidHandlerBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, ErgosfareContext context)
    {
        context.Mark("handler", command.Payload);

        if (command.Payload.Contains(SyncVocabulary.Throw, StringComparison.Ordinal))
        {
            throw new SyncFailure("sync-area void handler failed");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Aborts when asked, otherwise hands the handler a rewritten message.</summary>
[ExcludeFromDiscovery]
public abstract class SyncVoidPreBase<TCommand> : ICommand, IPreInterceptor<TCommand>
    where TCommand : class, ISyncPayloadCommand, new()
{
    /// <inheritdoc />
    public object Handle(TCommand message, ErgosfareContext context)
    {
        context.Mark("pre", message.Payload);

        if (message.Payload.Contains(SyncVocabulary.Abort, StringComparison.Ordinal))
        {
            context.Abort();
        }

        return new TCommand { Payload = message.Payload + SyncVocabulary.Rewritten };
    }
}

/// <summary>Records the message and result a synchronous void post-interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class SyncVoidPostBase<TCommand> : ICommand, IPostInterceptor<TCommand, Unit>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public object Handle(TCommand message, Unit messageResult, ErgosfareContext context)
    {
        context.Mark("post", $"{message.Payload}|{SyncVocabulary.Describe(messageResult)}");
        return messageResult;
    }
}

/// <summary>
/// Records everything a synchronous void exception interceptor is handed — when it is
/// reached at all. There is no result-agnostic synchronous flavor, so the void pipeline's
/// result slot meets a <see cref="Unit"/>-typed parameter here; before the handler has run
/// there is nothing in it and the parameter arrives <c>null</c>.
/// </summary>
[ExcludeFromDiscovery]
public abstract class SyncVoidExceptionBase<TCommand> : ICommand, IExceptionInterceptor<TCommand, Unit>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public object? Handle(TCommand message, Unit? messageResult, Exception exception, ErgosfareContext context)
    {
        context.Mark("exception",
            $"{message.Payload}|{SyncVocabulary.Describe(messageResult)}|{SyncVocabulary.Describe(exception)}");

        return messageResult;
    }
}

/// <inheritdoc cref="SyncVoidExceptionBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class SyncVoidFinalBase<TCommand> : ICommand, IFinalInterceptor<TCommand, Unit>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public void Handle(TCommand message, Unit? result, Exception? exception, ErgosfareContext executionContext)
        => executionContext.Mark("final",
            $"{message.Payload}|{SyncVocabulary.Describe(result)}|{SyncVocabulary.Describe(exception)}");
}

// ---------------------------------------------------------------------------
// string-result pipeline — asynchronous handler, synchronous interceptors
// ---------------------------------------------------------------------------

/// <inheritdoc cref="SyncVoidHandlerBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class SyncResultHandlerBase<TCommand> : ICommandHandler<TCommand, string>
    where TCommand : class, ISyncPayloadResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, ErgosfareContext context)
    {
        context.Mark("handler", command.Payload);

        if (command.Payload.Contains(SyncVocabulary.Throw, StringComparison.Ordinal))
        {
            throw new SyncFailure("sync-area result handler failed");
        }

        return ValueTask.FromResult(command.Payload);
    }
}

/// <inheritdoc cref="SyncVoidPreBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class SyncResultPreBase<TCommand> : ICommand, IPreInterceptor<TCommand>
    where TCommand : class, ISyncPayloadResultCommand, new()
{
    /// <inheritdoc />
    public object Handle(TCommand message, ErgosfareContext context)
    {
        context.Mark("pre", message.Payload);

        if (message.Payload.Contains(SyncVocabulary.Abort, StringComparison.Ordinal))
        {
            context.Abort();
        }

        return new TCommand { Payload = message.Payload + SyncVocabulary.Rewritten };
    }
}

/// <summary>Rewrites the handler's result on its way to the caller.</summary>
[ExcludeFromDiscovery]
public abstract class SyncResultPostBase<TCommand> : ICommand, IPostInterceptor<TCommand, string>
    where TCommand : class, ISyncPayloadResultCommand
{
    /// <inheritdoc />
    public object Handle(TCommand message, string messageResult, ErgosfareContext context)
    {
        context.Mark("post", $"{message.Payload}|{messageResult}");
        return messageResult + SyncVocabulary.Posted;
    }
}

/// <summary>Substitutes a fallback result for the handler's exception.</summary>
[ExcludeFromDiscovery]
public abstract class SyncResultExceptionBase<TCommand> : ICommand, IExceptionInterceptor<TCommand, string>
    where TCommand : class, ISyncPayloadResultCommand
{
    /// <inheritdoc />
    public object Handle(TCommand message, string? messageResult, Exception exception, ErgosfareContext context)
    {
        context.Mark("exception",
            $"{message.Payload}|{SyncVocabulary.Describe(messageResult)}|{SyncVocabulary.Describe(exception)}");

        return SyncVocabulary.StringFallback;
    }
}

/// <summary>Records everything a synchronous string-result final interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class SyncResultFinalBase<TCommand> : ICommand, IFinalInterceptor<TCommand, string>
    where TCommand : class, ISyncPayloadResultCommand
{
    /// <inheritdoc />
    public void Handle(TCommand message, string? result, Exception? exception, ErgosfareContext executionContext)
        => executionContext.Mark("final",
            $"{message.Payload}|{SyncVocabulary.Describe(result)}|{SyncVocabulary.Describe(exception)}");
}

// ---------------------------------------------------------------------------
// stage ordering across the two flavors
// ---------------------------------------------------------------------------

/// <summary>A synchronous pre-interceptor that only announces which slot it is.</summary>
[ExcludeFromDiscovery]
public abstract class SyncOrderedPreBase<TCommand>(string slot) : ICommand, IPreInterceptor<TCommand>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public object Handle(TCommand message, ErgosfareContext context)
    {
        context.Mark(slot);
        return message;
    }
}

/// <summary>An asynchronous pre-interceptor that only announces which slot it is.</summary>
[ExcludeFromDiscovery]
public abstract class AsyncOrderedPreBase<TCommand>(string slot) : ICommandPreInterceptor<TCommand>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
    {
        context.Mark(slot);
        return ValueTask.FromResult(command);
    }
}

/// <summary>A synchronous post-interceptor that only announces which slot it is.</summary>
[ExcludeFromDiscovery]
public abstract class SyncOrderedPostBase<TCommand>(string slot) : ICommand, IPostInterceptor<TCommand, Unit>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public object Handle(TCommand message, Unit messageResult, ErgosfareContext context)
    {
        context.Mark(slot);
        return messageResult;
    }
}

/// <summary>An asynchronous post-interceptor that only announces which slot it is.</summary>
[ExcludeFromDiscovery]
public abstract class AsyncOrderedPostBase<TCommand>(string slot) : ICommandPostInterceptor<TCommand>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object result, ErgosfareContext context)
    {
        context.Mark(slot);
        return ValueTask.FromResult(result);
    }
}

// ---------------------------------------------------------------------------
// the pre-Unit result key
// ---------------------------------------------------------------------------

/// <summary>
/// A synchronous void post-interceptor still written against the result key a resultless
/// pipeline used to carry. It compiles and it registers — the registry has no opinion on
/// result types — but the pipeline closes its stages over <see cref="Unit"/> now, so no
/// pattern-match arm claims this contract and the dispatch fails loudly rather than
/// skipping the stage. That noise is the point: the migration is a compile-time-invisible
/// key change, and silence would let a stage quietly stop running.
/// </summary>
[ExcludeFromDiscovery]
public abstract class StaleKeyVoidPostBase<TCommand> : ICommand, IPostInterceptor<TCommand, ValueTask>
    where TCommand : class, ISyncPayloadCommand
{
    /// <inheritdoc />
    public object Handle(TCommand message, ValueTask messageResult, ErgosfareContext context)
    {
        context.Mark("post:stale");
        return messageResult;
    }
}
