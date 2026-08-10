using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Contract.Test.ExceptionFilters;

/// <summary>
/// The shared vocabulary of the typed exception-interceptor scenarios: a pipeline that
/// throws a chosen exception type, and an exception stage whose participants each declare
/// which exceptions they accept.
/// </summary>
/// <remarks>
/// Everything here is excluded from discovery: these are shapes, not registrable
/// constructs. Exclusion is not inherited, so the closing types stay discoverable.
/// </remarks>
public static class ExceptionFilterVocabulary
{
    /// <summary>The result the string-result handler would have produced had it not thrown.</summary>
    public const string NeverProduced = "never";

    /// <summary>The result the interceptor accepting the tagged fault substitutes.</summary>
    public const string TaggedRecovery = "recovered:tagged";

    /// <summary>The suffix the unfiltered interceptor appends to whatever it is handed.</summary>
    public const string UntypedSuffix = "+untyped";

    /// <summary>Renders a stage's exception argument for the recorder.</summary>
    public static string Describe(Exception? exception)
        => exception is null ? "none" : exception.GetType().Name;
}

/// <summary>Which exception a scenario's handler throws.</summary>
public enum FaultKind
{
    /// <summary>The type the filtered interceptors are written for.</summary>
    Tagged,

    /// <summary>A subtype of it — accepted too, under <c>catch</c> semantics.</summary>
    DerivedTagged,

    /// <summary>A type only the second filtered interceptor accepts.</summary>
    Unrelated,

    /// <summary>A type no filtered interceptor accepts.</summary>
    Stray,
}

/// <summary>The fault the filtered interceptors of this area are written for.</summary>
/// <remarks>Deliberately not sealed: the subtype below is what pins <c>catch</c> matching.</remarks>
public class TaggedFaultException(string message = "tagged") : Exception(message);

/// <summary>A subtype of the tagged fault, used to pin <c>catch</c> matching semantics.</summary>
public sealed class DerivedTaggedFaultException() : TaggedFaultException("derived");

/// <summary>A fault only the second filtered interceptor of this area accepts.</summary>
public sealed class UnrelatedFaultException() : Exception("unrelated");

/// <summary>A fault no filtered interceptor of this area accepts.</summary>
public sealed class StrayFaultException() : Exception("stray");

/// <summary>Throws the fault the message selected.</summary>
public static class Faults
{
    /// <summary>Creates the exception a scenario asked its handler to throw.</summary>
    public static Exception Create(FaultKind kind) => kind switch
    {
        FaultKind.Tagged => new TaggedFaultException(),
        FaultKind.DerivedTagged => new DerivedTaggedFaultException(),
        FaultKind.Unrelated => new UnrelatedFaultException(),
        _ => new StrayFaultException(),
    };
}

/// <summary>The void command whose handler throws the fault it carries.</summary>
[ExcludeFromDiscovery]
public interface IFilteredVoidCommand : ICommand
{
    /// <summary>The fault the handler throws.</summary>
    FaultKind Fault { get; }
}

/// <summary>The string-result command whose handler throws the fault it carries.</summary>
[ExcludeFromDiscovery]
public interface IFilteredResultCommand : ICommand<string>
{
    /// <summary>The fault the handler throws.</summary>
    FaultKind Fault { get; }
}

// ---------------------------------------------------------------------------
// void pipeline — every exception-stage participant is filtered
// ---------------------------------------------------------------------------

/// <inheritdoc />
[ExcludeFromDiscovery]
public abstract class FilteredVoidHandlerBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : class, IFilteredVoidCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler");
        throw Faults.Create(command.Fault);
    }
}

/// <summary>Accepts the tagged fault and every type derived from it.</summary>
[ExcludeFromDiscovery]
public abstract class FilteredVoidTaggedBase<TCommand> : ICommandExceptionInterceptorFor<TCommand, TaggedFaultException>
    where TCommand : class, IFilteredVoidCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(
        TCommand command, object? messageResult, TaggedFaultException exception, IExecutionContext context)
    {
        context.Mark("exception:tagged", ExceptionFilterVocabulary.Describe(exception));
        return ValueTask.FromResult<object>(Unit.Value);
    }
}

/// <summary>Accepts only the unrelated fault.</summary>
[ExcludeFromDiscovery]
public abstract class FilteredVoidUnrelatedBase<TCommand> : ICommandExceptionInterceptorFor<TCommand, UnrelatedFaultException>
    where TCommand : class, IFilteredVoidCommand
{
    /// <inheritdoc />
    public ValueTask<object> HandleAsync(
        TCommand command, object? messageResult, UnrelatedFaultException exception, IExecutionContext context)
    {
        context.Mark("exception:unrelated", ExceptionFilterVocabulary.Describe(exception));
        return ValueTask.FromResult<object>(Unit.Value);
    }
}

/// <summary>Records the result and exception the final stage is handed.</summary>
[ExcludeFromDiscovery]
public abstract class FilteredVoidFinalBase<TCommand> : ICommandFinalInterceptor<TCommand>
    where TCommand : class, IFilteredVoidCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, object? result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final", ExceptionFilterVocabulary.Describe(exception));
        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// string-result pipeline — a filtered participant beside an unfiltered one
// ---------------------------------------------------------------------------

/// <inheritdoc />
[ExcludeFromDiscovery]
public abstract class FilteredResultHandlerBase<TCommand> : ICommandHandler<TCommand, string>
    where TCommand : class, IFilteredResultCommand
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(TCommand command, IExecutionContext context)
    {
        context.Mark("handler");
        throw Faults.Create(command.Fault);
    }
}

/// <summary>The heavier slot: accepts the tagged fault and substitutes a recovery result.</summary>
[ExcludeFromDiscovery]
public abstract class FilteredResultTaggedBase<TCommand>
    : ICommandExceptionInterceptorFor<TCommand, string, TaggedFaultException>
    where TCommand : class, IFilteredResultCommand
{
    /// <inheritdoc />
    public ValueTask<string?> HandleAsync(
        TCommand command, string? result, TaggedFaultException exception, IExecutionContext context)
    {
        context.Mark("exception:tagged", ExceptionFilterVocabulary.Describe(exception));
        return ValueTask.FromResult<string?>(ExceptionFilterVocabulary.TaggedRecovery);
    }
}

/// <summary>The lighter slot: unfiltered, so it accepts whatever reaches the stage.</summary>
[ExcludeFromDiscovery]
public abstract class FilteredResultUntypedBase<TCommand> : ICommandExceptionInterceptor<TCommand, string>
    where TCommand : class, IFilteredResultCommand
{
    /// <inheritdoc />
    public ValueTask<string?> HandleAsync(
        TCommand command, string? result, Exception exception, IExecutionContext context)
    {
        context.Mark("exception:untyped", ExceptionFilterVocabulary.Describe(exception));
        return ValueTask.FromResult<string?>((result ?? string.Empty) + ExceptionFilterVocabulary.UntypedSuffix);
    }
}

/// <inheritdoc cref="FilteredVoidFinalBase{TCommand}"/>
[ExcludeFromDiscovery]
public abstract class FilteredResultFinalBase<TCommand> : ICommandFinalInterceptor<TCommand, string>
    where TCommand : class, IFilteredResultCommand
{
    /// <inheritdoc />
    public ValueTask HandleAsync(TCommand command, string? result, Exception? exception, IExecutionContext context)
    {
        context.Mark("final", ExceptionFilterVocabulary.Describe(exception));
        return ValueTask.CompletedTask;
    }
}
