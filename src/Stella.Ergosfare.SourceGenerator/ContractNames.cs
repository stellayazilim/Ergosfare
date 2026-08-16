namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     The Ergosfare surface as the symbol layer matches it: the three module markers every
///     construct inherits through its contracts, and the namespaces the contracts and
///     attributes live in. Matching on name plus namespace rather than on a metadata lookup
///     is deliberate — a consumer's own <c>ICommand</c> must not be mistaken for ours.
/// </summary>
internal static class ContractNames
{
    internal const string CommandMarker = "ICommand";
    internal const string CommandMarkerNamespace = "Stella.Ergosfare.Commands.Abstractions";

    internal const string QueryMarker = "IQuery";
    internal const string QueryMarkerNamespace = "Stella.Ergosfare.Queries.Abstractions";

    internal const string EventMarker = "IEvent";
    internal const string EventMarkerNamespace = "Stella.Ergosfare.Events.Abstractions";

    /// <summary>
    ///     The subscriber contract. Its type argument is what makes a plain type an event
    ///     message: the publish lane is declared over <c>notnull</c> end to end, so a domain
    ///     type needs no marker of its own to travel it.
    /// </summary>
    internal const string EventHandlerContract = "IEventHandler";

    internal const string HandlerNamespace = "Stella.Ergosfare.Core.Abstractions.Handlers";
    internal const string AttributeNamespace = "Stella.Ergosfare.Core.Abstractions.Attributes";
    internal const string CoreAbstractionsNamespace = "Stella.Ergosfare.Core.Abstractions";

    internal const string ExceptionFilterContract = "IExceptionInterceptorFilter";
}
