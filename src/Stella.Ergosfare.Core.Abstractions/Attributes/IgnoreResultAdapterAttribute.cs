using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Opts a message type out of result adaptation entirely: no annotation binding, no
/// built-in <see cref="Result"/>/<see cref="Result{TValue}"/> adapter, no configured
/// default adapter — the message's pipelines keep the classic try/catch semantics, and
/// the dispatch path performs no probing at all. The escape hatch for applications that
/// configure a default adapter but want individual messages off the value channel.
/// </summary>
/// <remarks>
/// Inherited like <see cref="ResultAdapterAttribute"/>: an annotation on a base message
/// type covers its derived messages. Declaring both this attribute and
/// <see cref="ResultAdapterAttribute"/> on the same message (own or inherited, in any
/// combination) is contradictory and fails the build (ERGO012); against assemblies
/// compiled before that rule, the runtime binding lets the opt-out win.
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class IgnoreResultAdapterAttribute : Attribute;
