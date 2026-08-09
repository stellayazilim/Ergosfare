using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Pipeline;

/// <summary>
/// The pipeline scenarios' shared vocabulary and behavior. Each registration axis closes
/// these bases over its own concrete message types; the axes therefore run byte-identical
/// participant code and any difference the scenarios see is a difference in registration,
/// not in the handlers.
/// </summary>
/// <remarks>
/// Everything in this file is excluded from discovery: these are shapes, not registrable
/// constructs, and the generated axis must register only the concrete types it declares.
/// Exclusion is not inherited (<c>Inherited = false</c>), so the closing types stay
/// discoverable.
/// </remarks>
public static class PipelineVocabulary
{
    /// <summary>Payload marker that makes a handler throw.</summary>
    public const string Throw = "throw";

    /// <summary>Payload marker that makes a pre-interceptor abort.</summary>
    public const string Abort = "abort";

    /// <summary>Suffix a pre-interceptor appends when it rewrites the message.</summary>
    public const string Rewritten = "+rewritten";

    /// <summary>Suffix a post-interceptor appends when it rewrites a string result.</summary>
    public const string Posted = "+posted";

    /// <summary>What a value-typed post-interceptor adds to the handler's number.</summary>
    public const int PostAddend = 100;

    /// <summary>What a value-typed exception interceptor falls back to.</summary>
    public const int ValueFallback = -1;

    /// <summary>What a string exception interceptor falls back to.</summary>
    public const string StringFallback = "fallback";

    /// <summary>The number a value query's handler produces.</summary>
    public const int HandlerValue = 7;

    /// <summary>
    /// Renders a stage's result argument for the recorder. The <see cref="Unit"/> arm
    /// compares by reference on purpose: a resultless pipeline must hand every stage the
    /// one shared instance, and <c>"unit:other"</c> is what a scenario would print if it
    /// ever stopped doing so.
    /// </summary>
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
public sealed class PipelineFailure(string message) : Exception(message);

/// <summary>A void command carrying the payload that drives its scenario.</summary>
[ExcludeFromDiscovery]
public interface IPayloadCommand : ICommand
{
    /// <summary>Drives handler/interceptor behavior and records what each stage saw.</summary>
    string Payload { get; set; }
}

/// <summary>A string-result command carrying the payload that drives its scenario.</summary>
[ExcludeFromDiscovery]
public interface IPayloadResultCommand : ICommand<string>
{
    /// <inheritdoc cref="IPayloadCommand.Payload"/>
    string Payload { get; set; }
}

/// <summary>A value-typed query carrying the payload that drives its scenario.</summary>
[ExcludeFromDiscovery]
public interface IPayloadValueQuery : IQuery<int>
{
    /// <inheritdoc cref="IPayloadCommand.Payload"/>
    string Payload { get; set; }
}

// ---------------------------------------------------------------------------
// void command pipeline
// ---------------------------------------------------------------------------

/// <summary>Marks its stage, then throws when the payload asks it to.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadHandlerBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, IPayloadCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler", command.Payload);

        if (command.Payload.Contains(PipelineVocabulary.Throw, StringComparison.Ordinal))
        {
            throw new PipelineFailure("void handler failed");
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Aborts when asked, otherwise hands the handler a rewritten message.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadPreBase<TCommand> : ICommandPreInterceptor<TCommand>
    where TCommand : class, IPayloadCommand, new()
{
    /// <inheritdoc />
    public ValueTask<TCommand> HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("pre", command.Payload);

        if (command.Payload.Contains(PipelineVocabulary.Abort, StringComparison.Ordinal))
        {
            context.Abort();
        }

        return ValueTask.FromResult(new TCommand { Payload = command.Payload + PipelineVocabulary.Rewritten });
    }
}

/// <summary>Records the message and result a void post-interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadPostBase<TCommand> : ICommandPostInterceptor<TCommand>
    where TCommand : class, IPayloadCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object result, IExecutionContext context)
    {
        context.Mark("post", $"{command.Payload}|{PipelineVocabulary.Describe(result)}");
        return ValueTask.FromResult(result);
    }
}

/// <summary>Records everything a void exception interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadExceptionBase<TCommand> : ICommandExceptionInterceptor<TCommand>
    where TCommand : class, IPayloadCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object? result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception",
            $"{command.Payload}|{PipelineVocabulary.Describe(result)}|{PipelineVocabulary.Describe(exception)}");

        return ValueTask.FromResult<object>(Unit.Value);
    }
}

/// <summary>Records everything a void final interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadFinalBase<TCommand> : ICommandFinalInterceptor<TCommand>
    where TCommand : class, IPayloadCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, object? result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final",
            $"{command.Payload}|{PipelineVocabulary.Describe(result)}|{PipelineVocabulary.Describe(exception)}");

        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// string-result command pipeline
// ---------------------------------------------------------------------------

/// <summary>Returns its received payload as the result, or throws when asked.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadResultHandlerBase<TCommand> : ICommandHandler<TCommand, string>
    where TCommand : class, IPayloadResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler", command.Payload);

        if (command.Payload.Contains(PipelineVocabulary.Throw, StringComparison.Ordinal))
        {
            throw new PipelineFailure("result handler failed");
        }

        return ValueTask.FromResult(command.Payload);
    }
}

/// <summary>Aborts when asked — with a result value, to pin whether the value survives.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadResultPreBase<TCommand> : ICommandPreInterceptor<TCommand>
    where TCommand : class, IPayloadResultCommand, new()
{
    /// <summary>The value handed to <see cref="IExecutionContext.Abort"/> on the abort path.</summary>
    public const string AbortValue = "aborted-with-value";

    /// <inheritdoc />
    public ValueTask<TCommand> HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("pre", command.Payload);

        if (command.Payload.Contains(PipelineVocabulary.Abort, StringComparison.Ordinal))
        {
            context.Abort(AbortValue);
        }

        return ValueTask.FromResult(new TCommand { Payload = command.Payload + PipelineVocabulary.Rewritten });
    }
}

/// <summary>Rewrites the handler's result on its way to the caller.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadResultPostBase<TCommand> : ICommandPostInterceptor<TCommand, string>
    where TCommand : class, IPayloadResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, string result, IExecutionContext context)
    {
        context.Mark("post", $"{command.Payload}|{result}");
        return ValueTask.FromResult(result + PipelineVocabulary.Posted);
    }
}

/// <summary>Substitutes a fallback result for the handler's exception.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadResultExceptionBase<TCommand> : ICommandExceptionInterceptor<TCommand, string>
    where TCommand : class, IPayloadResultCommand
{
    /// <inheritdoc />
    public ValueTask<string?> HandleAsync(TCommand command, string? result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception",
            $"{command.Payload}|{PipelineVocabulary.Describe(result)}|{PipelineVocabulary.Describe(exception)}");

        return ValueTask.FromResult<string?>(PipelineVocabulary.StringFallback);
    }
}

/// <summary>Records everything a string-result final interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadResultFinalBase<TCommand> : ICommandFinalInterceptor<TCommand, string>
    where TCommand : class, IPayloadResultCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, string? result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final",
            $"{command.Payload}|{PipelineVocabulary.Describe(result)}|{PipelineVocabulary.Describe(exception)}");

        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// value-typed query pipeline
// ---------------------------------------------------------------------------

/// <summary>Produces a number, or throws when asked.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadValueHandlerBase<TQuery> : IQueryHandler<TQuery, int>
    where TQuery : class, IPayloadValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, IExecutionContext context)
    {
        context.Mark("handler", query.Payload);

        if (query.Payload.Contains(PipelineVocabulary.Throw, StringComparison.Ordinal))
        {
            throw new PipelineFailure("value handler failed");
        }

        return ValueTask.FromResult(PipelineVocabulary.HandlerValue);
    }
}

/// <summary>Aborts when asked, otherwise hands the handler a rewritten query.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadValuePreBase<TQuery> : IQueryPreInterceptor<TQuery>
    where TQuery : class, IPayloadValueQuery, new()
{
    /// <summary>The value handed to <see cref="IExecutionContext.Abort"/> on the abort path.</summary>
    public const int AbortValue = 41;

    /// <inheritdoc />
    public ValueTask<TQuery> HandleAsync(TQuery query, IExecutionContext context)
    {
        context.Mark("pre", query.Payload);

        if (query.Payload.Contains(PipelineVocabulary.Abort, StringComparison.Ordinal))
        {
            context.Abort(AbortValue);
        }

        return ValueTask.FromResult(new TQuery { Payload = query.Payload + PipelineVocabulary.Rewritten });
    }
}

/// <summary>Adds to the handler's number on its way to the caller.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadValuePostBase<TQuery> : IQueryPostInterceptor<TQuery, int>
    where TQuery : class, IPayloadValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, int result, IExecutionContext context)
    {
        context.Mark("post", $"{query.Payload}|{result}");
        return ValueTask.FromResult(result + PipelineVocabulary.PostAddend);
    }
}

/// <summary>Substitutes a fallback number for the handler's exception.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadValueExceptionBase<TQuery> : IQueryExceptionInterceptor<TQuery, int>
    where TQuery : class, IPayloadValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, int result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception",
            $"{query.Payload}|{result}|{PipelineVocabulary.Describe(exception)}");

        return ValueTask.FromResult(PipelineVocabulary.ValueFallback);
    }
}

/// <summary>Records everything a value-typed final interceptor is handed.</summary>
[ExcludeFromDiscovery]
public abstract class PayloadValueFinalBase<TQuery> : IQueryFinalInterceptor<TQuery, int>
    where TQuery : class, IPayloadValueQuery
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TQuery query, int result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final", $"{query.Payload}|{result}|{PipelineVocabulary.Describe(exception)}");
        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// stage ordering
// ---------------------------------------------------------------------------

/// <summary>Marks the handler of the ordering scenario.</summary>
[ExcludeFromDiscovery]
public abstract class OrderedHandlerBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>A pre-interceptor that only announces which slot it is.</summary>
[ExcludeFromDiscovery]
public abstract class OrderedPreBase<TCommand>(string slot) : ICommandPreInterceptor<TCommand>
    where TCommand : class, ICommand
{
    /// <inheritdoc />
    public ValueTask<TCommand> HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark(slot);
        return ValueTask.FromResult(command);
    }
}

/// <summary>A post-interceptor that only announces which slot it is.</summary>
[ExcludeFromDiscovery]
public abstract class OrderedPostBase<TCommand>(string slot) : ICommandPostInterceptor<TCommand>
    where TCommand : class, ICommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object result, IExecutionContext context)
    {
        context.Mark(slot);
        return ValueTask.FromResult(result);
    }
}
