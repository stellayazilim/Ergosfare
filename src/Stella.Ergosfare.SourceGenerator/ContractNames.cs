namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The names the symbol layer matches Ergosfare's own types by.
/// </summary>
/// <remarks>
/// Types are matched on name plus namespace rather than looked up through a reference, so
/// that an application's own <c>ICommand</c> is never mistaken for this one.
/// </remarks>
internal static class ContractNames
{
    /// <summary>The marker every command construct carries.</summary>
    internal const string CommandMarker = "ICommand";

    /// <summary>The namespace the command marker lives in.</summary>
    internal const string CommandMarkerNamespace = "Stella.Ergosfare.Commands.Abstractions";

    /// <summary>The marker every query construct carries.</summary>
    internal const string QueryMarker = "IQuery";

    /// <summary>The namespace the query marker lives in.</summary>
    internal const string QueryMarkerNamespace = "Stella.Ergosfare.Queries.Abstractions";

    /// <summary>The marker every event construct carries.</summary>
    internal const string EventMarker = "IEvent";

    /// <summary>The namespace the event marker lives in.</summary>
    internal const string EventMarkerNamespace = "Stella.Ergosfare.Events.Abstractions";

    /// <summary>
    /// The subscriber contract.
    /// </summary>
    /// <remarks>
    /// Its type argument is what makes a plain type an event: the publish path is declared
    /// over non-null types end to end, so a domain type needs no marker of its own to
    /// travel it.
    /// </remarks>
    internal const string EventHandlerContract = "IEventHandler";

    /// <summary>The namespace the pipeline contracts live in.</summary>
    internal const string HandlerNamespace = "Stella.Ergosfare.Core.Abstractions.Handlers";

    /// <summary>The namespace the pipeline attributes live in.</summary>
    internal const string AttributeNamespace = "Stella.Ergosfare.Core.Abstractions.Attributes";

    /// <summary>The namespace the core abstractions live in.</summary>
    internal const string CoreAbstractionsNamespace = "Stella.Ergosfare.Core.Abstractions";

    /// <summary>The contract an interceptor carries to accept only some failures.</summary>
    internal const string ExceptionFilterContract = "IExceptionInterceptorFilter";
}
