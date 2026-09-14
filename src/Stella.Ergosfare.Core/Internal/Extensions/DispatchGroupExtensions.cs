using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Extensions;

/// <summary>Normalizes group inputs for repeated reads during lookup and plan execution.</summary>
internal static class DispatchGroupExtensions
{
    internal static readonly string[] Default = [GroupAttribute.DefaultGroupName];

    internal static IReadOnlyList<string> ToDispatchGroups(this GroupSet? groups)
        => groups ?? GroupSet.Empty;
}
