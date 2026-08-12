namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// The dispatch surface a recorded dispatch site went through; see
/// <see cref="DispatchSiteAttribute"/>.
/// </summary>
public enum DispatchKind : byte
{
    /// <summary>An <c>ICommandMediator.SendAsync</c> dispatch.</summary>
    Command = 0,

    /// <summary>An <c>IQueryMediator.QueryAsync</c> dispatch.</summary>
    Query = 1,

    /// <summary>An <c>IQueryMediator.StreamAsync</c> dispatch.</summary>
    Stream = 2,

    /// <summary>An <c>IEventMediator.PublishAsync</c> dispatch.</summary>
    Event = 3,

    /// <summary>A core <c>IMessageMediator</c> dispatch (module-agnostic).</summary>
    Message = 4,
}
