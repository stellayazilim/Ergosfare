namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Which mediator surface a recorded dispatch site called; see
/// <see cref="DispatchSiteAttribute"/>.
/// </summary>
public enum DispatchKind : byte
{
    /// <summary>A call to <c>ICommandMediator.SendAsync</c>.</summary>
    Command = 0,

    /// <summary>A call to <c>IQueryMediator.QueryAsync</c>.</summary>
    Query = 1,

    /// <summary>A call to <c>IQueryMediator.StreamAsync</c>.</summary>
    Stream = 2,

    /// <summary>A call to <c>IEventMediator.PublishAsync</c>.</summary>
    Event = 3,

    /// <summary>A call to <c>IMessageMediator</c>, which belongs to no module.</summary>
    Message = 4,
}
