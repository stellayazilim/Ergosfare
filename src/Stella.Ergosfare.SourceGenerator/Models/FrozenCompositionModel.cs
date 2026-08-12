using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>One participant row of a frozen composition, ready for emission.</summary>
/// <param name="TypeofExpression">The participant's <c>typeof</c> argument (unbound form for definitions).</param>
/// <param name="GroupsExpression">The emitted group-array expression, or <c>null</c> for the default group alone.</param>
internal readonly record struct FrozenParticipantModel(string TypeofExpression, string? GroupsExpression);

/// <summary>
///     A message's frozen pipeline composition ready for emission: the two main-handler
///     segments and the four interceptor stages as direct/indirect segment pairs, each
///     segment already in the runtime shape-builder's execution order (weight descending,
///     then ordinal CLR <c>FullName</c>). Every message the generator can name gets an
///     entry, keyed participants included — which of the rows an application runs is
///     settled by what it registered, not by the table. A message's
///     <c>[ExcludeFromPipeline]</c> is applied here rather than at dispatch, and a
///     participant generated code could not reference at all (inaccessible) simply
///     contributes no row.
/// </summary>
internal sealed record FrozenCompositionModel(
    string MessageTypeExpression,
    ImmutableArray<FrozenParticipantModel> Handlers,
    ImmutableArray<FrozenParticipantModel> IndirectHandlers,
    ImmutableArray<FrozenParticipantModel> PreInterceptors,
    ImmutableArray<FrozenParticipantModel> IndirectPreInterceptors,
    ImmutableArray<FrozenParticipantModel> PostInterceptors,
    ImmutableArray<FrozenParticipantModel> IndirectPostInterceptors,
    ImmutableArray<FrozenParticipantModel> ExceptionInterceptors,
    ImmutableArray<FrozenParticipantModel> IndirectExceptionInterceptors,
    ImmutableArray<FrozenParticipantModel> FinalInterceptors,
    ImmutableArray<FrozenParticipantModel> IndirectFinalInterceptors);
