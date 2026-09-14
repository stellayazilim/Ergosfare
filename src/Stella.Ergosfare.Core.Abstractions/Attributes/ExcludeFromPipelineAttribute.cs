
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Detaches a message from interceptors it only matches covariantly — those registered
/// against one of its base types or interfaces.
/// </summary>
/// <remarks>
/// Interceptors registered against the message type itself always run; they were written
/// for this message. With no arguments every covariantly matched interceptor is detached;
/// with group names only those declaring one of the named <see cref="GroupAttribute"/>
/// groups are. Main handlers are never affected — this shapes the interceptor stages, not
/// handler selection.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
public sealed class ExcludeFromPipelineAttribute(params string[] groups) : Attribute
{
    /// <summary>
    /// The interceptor groups to detach; empty detaches every covariantly matched
    /// interceptor.
    /// </summary>
    public string[] Groups => groups;
}
