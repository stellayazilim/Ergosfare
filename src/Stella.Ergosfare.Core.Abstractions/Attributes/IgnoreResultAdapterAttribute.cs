using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Takes a message type out of result adaptation completely: no annotated adapter, no
/// built-in <see cref="Result"/> or <see cref="Result{TValue}"/> adapter, and no
/// application-wide default adapter applies to it.
/// </summary>
/// <remarks>
/// Its pipelines keep the default behavior — a failure is thrown rather than returned —
/// and the dispatch path never probes the result. Use it to keep individual messages off
/// the value channel in an application that configures a default adapter.
/// The attribute is inherited, like <see cref="ResultAdapterAttribute"/>. Carrying both on
/// one message, whether declared or inherited, is contradictory and fails the build with
/// ERGO012; where both reach the runtime, the opt-out wins.
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class IgnoreResultAdapterAttribute : Attribute;
