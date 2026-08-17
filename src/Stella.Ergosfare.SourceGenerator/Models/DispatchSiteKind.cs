
namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which mediator surface a discovered dispatch site calls.
/// </summary>
/// <remarks>
/// The generator's copy of
/// <c>Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchKind</c>. It cannot reference
/// the runtime assembly, so it reads the numbers and these must stay identical to that
/// enum's.
/// </remarks>
internal enum DispatchSiteKind : byte
{
    /// <summary>A call to the command mediator.</summary>
    Command = 0,

    /// <summary>A call to the query mediator.</summary>
    Query = 1,

    /// <summary>A streaming call to the query mediator.</summary>
    Stream = 2,

    /// <summary>A call to the event mediator.</summary>
    Event = 3,

    /// <summary>A call to the core message mediator.</summary>
    Message = 4,
}
