
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Keeps a type out of automatic discovery. Generated registration skips it whatever
/// discovery key or pattern is requested.
/// </summary>
/// <remarks>
/// Explicit registration is unaffected: <c>Register&lt;T&gt;()</c> and
/// <c>Register(Type)</c> still register the type. Applied to an assembly, the attribute
/// excludes every type in it.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Assembly,
    Inherited = false)]
public sealed class ExcludeFromDiscoveryAttribute : Attribute;
