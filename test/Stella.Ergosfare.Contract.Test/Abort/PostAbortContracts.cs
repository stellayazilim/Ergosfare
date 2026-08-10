using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Abort;

/// <summary>
/// The shared vocabulary and behavior of the post-interceptor abort scenarios: a pipeline
/// that has already produced a result, aborted from the stage that runs after the handler.
/// </summary>
/// <remarks>
/// Everything here is excluded from discovery: these are shapes, not registrable
/// constructs. Exclusion is not inherited, so the closing types stay discoverable.
/// </remarks>
public static class AbortVocabulary
{
    /// <summary>The value the string-result handler produces before the abort.</summary>
    public const string HandlerResult = "produced";

    /// <summary>The value the query handler produces before the abort.</summary>
    public const int HandlerValue = 7;

    /// <summary>What the aborting post-interceptor would have returned had it returned.</summary>
    public const string NeverPosted = "+never";

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

/// <summary>The void command whose post stage aborts.</summary>
[ExcludeFromDiscovery]
public interface IAbortCommand : ICommand;

/// <summary>The string-result command whose post stage aborts.</summary>
[ExcludeFromDiscovery]
public interface IAbortResultCommand : ICommand<string>;

/// <summary>The value-typed query whose post stage aborts.</summary>
[ExcludeFromDiscovery]
public interface IAbortValueQuery : IQuery<int>;

// ---------------------------------------------------------------------------
// void pipeline
// ---------------------------------------------------------------------------

/// <inheritdoc />
[ExcludeFromDiscovery]
public abstract class AbortVoidHandlerBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, IAbortCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Aborts after the handler has run — the heaviest post slot, so it runs first.</summary>
[ExcludeFromDiscovery]
public abstract class AbortingVoidPostBase<TCommand> : ICommandPostInterceptor<TCommand>
    where TCommand : class, IAbortCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object result, IExecutionContext context)
    {
        context.Mark("post:abort", AbortVocabulary.Describe(result));
        context.Abort();
        return ValueTask.FromResult(result);
    }
}

/// <summary>The lighter post slot: it marks only if the abort did not stop the stage.</summary>
[ExcludeFromDiscovery]
public abstract class LateVoidPostBase<TCommand> : ICommandPostInterceptor<TCommand>
    where TCommand : class, IAbortCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object result, IExecutionContext context)
    {
        context.Mark("post:after");
        return ValueTask.FromResult(result);
    }
}

/// <summary>Marks only if an abort is routed through the exception stage.</summary>
[ExcludeFromDiscovery]
public abstract class AbortVoidExceptionBase<TCommand> : ICommandExceptionInterceptor<TCommand>
    where TCommand : class, IAbortCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(TCommand command, object? result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception", AbortVocabulary.Describe(exception));
        return ValueTask.FromResult<object>(Unit.Value);
    }
}

/// <summary>Records the result and exception the final stage is handed after the abort.</summary>
[ExcludeFromDiscovery]
public abstract class AbortVoidFinalBase<TCommand> : ICommandFinalInterceptor<TCommand>
    where TCommand : class, IAbortCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, object? result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final", $"{AbortVocabulary.Describe(result)}|{AbortVocabulary.Describe(exception)}");
        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// string-result pipeline
// ---------------------------------------------------------------------------

/// <inheritdoc />
[ExcludeFromDiscovery]
public abstract class AbortResultHandlerBase<TCommand> : ICommandHandler<TCommand, string>
    where TCommand : class, IAbortResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler");
        return ValueTask.FromResult(AbortVocabulary.HandlerResult);
    }
}

/// <inheritdoc cref="AbortingVoidPostBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class AbortingResultPostBase<TCommand> : ICommandPostInterceptor<TCommand, string>
    where TCommand : class, IAbortResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, string commandResult, IExecutionContext context)
    {
        context.Mark("post:abort", commandResult);
        context.Abort();
        return ValueTask.FromResult(commandResult + AbortVocabulary.NeverPosted);
    }
}

/// <inheritdoc cref="LateVoidPostBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class LateResultPostBase<TCommand> : ICommandPostInterceptor<TCommand, string>
    where TCommand : class, IAbortResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, string commandResult, IExecutionContext context)
    {
        context.Mark("post:after");
        return ValueTask.FromResult(commandResult);
    }
}

/// <inheritdoc cref="AbortVoidExceptionBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class AbortResultExceptionBase<TCommand> : ICommandExceptionInterceptor<TCommand, string>
    where TCommand : class, IAbortResultCommand
{
    /// <inheritdoc />
    public ValueTask<string?> HandleAsync(TCommand command, string? result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception", AbortVocabulary.Describe(exception));
        return ValueTask.FromResult<string?>(result);
    }
}

/// <inheritdoc cref="AbortVoidFinalBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class AbortResultFinalBase<TCommand> : ICommandFinalInterceptor<TCommand, string>
    where TCommand : class, IAbortResultCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, string? result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final", $"{AbortVocabulary.Describe(result)}|{AbortVocabulary.Describe(exception)}");
        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// value-typed query pipeline
// ---------------------------------------------------------------------------

/// <inheritdoc />
[ExcludeFromDiscovery]
public abstract class AbortQueryHandlerBase<TQuery> : IQueryHandler<TQuery, int>
    where TQuery : class, IAbortValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, IExecutionContext context)
    {
        context.Mark("handler");
        return ValueTask.FromResult(AbortVocabulary.HandlerValue);
    }
}

/// <inheritdoc cref="AbortingVoidPostBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class AbortingQueryPostBase<TQuery> : IQueryPostInterceptor<TQuery, int>
    where TQuery : class, IAbortValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, int queryResult, IExecutionContext context)
    {
        context.Mark("post:abort", queryResult.ToString());
        context.Abort();
        return ValueTask.FromResult(queryResult + 1);
    }
}

/// <inheritdoc cref="LateVoidPostBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class LateQueryPostBase<TQuery> : IQueryPostInterceptor<TQuery, int>
    where TQuery : class, IAbortValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, int queryResult, IExecutionContext context)
    {
        context.Mark("post:after");
        return ValueTask.FromResult(queryResult);
    }
}

/// <inheritdoc cref="AbortVoidExceptionBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class AbortQueryExceptionBase<TQuery> : IQueryExceptionInterceptor<TQuery, int>
    where TQuery : class, IAbortValueQuery
{
    /// <inheritdoc />
    public ValueTask<int> HandleAsync(TQuery query, int result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception", AbortVocabulary.Describe(exception));
        return ValueTask.FromResult(result);
    }
}

/// <inheritdoc cref="AbortVoidFinalBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class AbortQueryFinalBase<TQuery> : IQueryFinalInterceptor<TQuery, int>
    where TQuery : class, IAbortValueQuery
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TQuery query, int result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final", $"{result}|{AbortVocabulary.Describe(exception)}");
        return ValueTask.CompletedTask;
    }
}
