using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One hand-written registration — a <c>Register&lt;T&gt;()</c> or
/// <c>Register(typeof(T))</c> call — reduced to what reachability judgment needs.
/// </summary>
/// <remarks>
/// Registering by hand collects a type through the same generated path as
/// <c>RegisterGenerated()</c>, one at a time rather than in bulk, so the type's handler
/// contracts count as evidence that a message is handled exactly as a discovered handler
/// would. A registration whose type cannot be known at compile time — a <c>Type</c>
/// argument that is not a <c>typeof</c>, a batch of descriptors, or the assembly scan older
/// packages used — leaves that evidence incomplete, and judgment stops.
/// </remarks>
internal readonly struct RegistrationSiteModel : IEquatable<RegistrationSiteModel>
{
    /// <summary>
    /// The CLR metadata name of the registered type, or <c>null</c> when the type is
    /// unknown.
    /// </summary>
    public required string? TypeMetadataName { get; init; }

    /// <summary>
    /// The message types this registration proves are handled, normalized. Empty for a
    /// message, an interceptor, or an unknown type — none of them makes a message handled.
    /// </summary>
    public required ImmutableArray<string> MainHandlerMessageKeys { get; init; }

    /// <summary>
    /// Whether the registered type cannot be known at compile time.
    /// </summary>
    public required bool IsOpaque { get; init; }

    /// <summary>
    /// Where to report ERGO018, set only for a <c>Register</c> call naming a type this
    /// compilation cannot know.
    /// </summary>
    /// <remarks>
    /// The other unknowable shape — the generator's own bulk channel — leaves this
    /// <c>null</c>: its argument is a sequence of types by design, so it is unknowable
    /// without being a mistake.
    /// </remarks>
    public required LocationInfo? UnknownTypeLocation { get; init; }

    /// <summary>
    /// Compares the registered type, its evidence and its location.
    /// </summary>
    /// <param name="other">The site to compare against.</param>
    /// <returns><c>true</c> when both describe the same registration.</returns>
    public bool Equals(RegistrationSiteModel other)
    {
        if (TypeMetadataName != other.TypeMetadataName
            || IsOpaque != other.IsOpaque
            || !Nullable.Equals(UnknownTypeLocation, other.UnknownTypeLocation)
            || MainHandlerMessageKeys.Length != other.MainHandlerMessageKeys.Length)
        {
            return false;
        }

        for (var i = 0; i < MainHandlerMessageKeys.Length; i++)
        {
            if (!string.Equals(MainHandlerMessageKeys[i], other.MainHandlerMessageKeys[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares against another registration site.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is an equal site.</returns>
    public override bool Equals(object? obj) => obj is RegistrationSiteModel other && Equals(other);

    /// <summary>
    /// Returns a hash over the type name, how much evidence it carries, and whether it is
    /// unknowable.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            return ((TypeMetadataName?.GetHashCode() ?? 0) * 397)
                   ^ (MainHandlerMessageKeys.Length << 1)
                   ^ (IsOpaque ? 1 : 0);
        }
    }
}
