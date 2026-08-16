
namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     The dispatch surface a discovered dispatch site goes through. Mirrors the public
///     <c>Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchKind</c> values — the
///     generator cannot reference the runtime assembly, so the numeric values must stay
///     in lockstep with it.
/// </summary>
internal enum DispatchSiteKind : byte
{
    Command = 0,
    Query = 1,
    Stream = 2,
    Event = 3,
    Message = 4,
}
