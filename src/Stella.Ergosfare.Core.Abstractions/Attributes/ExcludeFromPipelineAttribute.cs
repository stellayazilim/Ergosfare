
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Excludes a message from covariantly matched interceptors: interceptors registered
/// against a base type or interface of the message (e.g. an <c>IEvent</c>-wide
/// pre-interceptor) no longer apply to it. Interceptors registered against the message
/// type itself always run — they were written for this message deliberately.
/// </summary>
/// <remarks>
/// Without arguments every covariantly matched interceptor is excluded; with group names
/// only the covariant interceptors carrying one of those <see cref="GroupAttribute"/>
/// groups are. Main handlers are never affected — the attribute shapes the interceptor
/// pipeline, not dispatch itself.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
public sealed class ExcludeFromPipelineAttribute(params string[] groups) : Attribute
{
    /// <summary>
    /// The interceptor groups excluded from covariant matching; empty to exclude every
    /// covariantly matched interceptor.
    /// </summary>
    public string[] Groups => groups;
}
