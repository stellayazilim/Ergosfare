
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Excludes a registrable construct from automatic discovery entirely: both
/// source-generated registration and reflection-based assembly scanning
/// (<c>RegisterFromAssembly</c>) skip the type, regardless of discovery keys or patterns.
/// Explicit registration (<c>Register&lt;T&gt;()</c>, <c>Register(Type)</c>) still works.
/// </summary>
/// <remarks>
/// Applied to an assembly, the attribute removes every type in that assembly from
/// discovery — for libraries that wire themselves up manually or are never meant to be
/// scanned.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    Inherited = false)]
public sealed class ExcludeFromDiscoveryAttribute : Attribute;
