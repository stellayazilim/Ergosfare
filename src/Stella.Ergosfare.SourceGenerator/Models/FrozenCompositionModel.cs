using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A message's whole compiled pipeline, ready to be written out: the main handlers and the
/// four interceptor stages, each split into the participants registered for the message
/// type itself and those registered for a base type.
/// </summary>
/// <param name="MessageTypeExpression">The message type this composition belongs to.</param>
/// <param name="Handlers">Main handlers registered for the message type itself.</param>
/// <param name="IndirectHandlers">Main handlers registered for a base type.</param>
/// <param name="PreInterceptors">Pre-interceptors registered for the message type itself.</param>
/// <param name="IndirectPreInterceptors">Pre-interceptors registered for a base type.</param>
/// <param name="PostInterceptors">Post-interceptors registered for the message type itself.</param>
/// <param name="IndirectPostInterceptors">Post-interceptors registered for a base type.</param>
/// <param name="ExceptionInterceptors">Exception interceptors registered for the message type itself.</param>
/// <param name="IndirectExceptionInterceptors">Exception interceptors registered for a base type.</param>
/// <param name="FinalInterceptors">Final interceptors registered for the message type itself.</param>
/// <param name="IndirectFinalInterceptors">Final interceptors registered for a base type.</param>
/// <remarks>
/// Every segment is already in invocation order — descending weight, then ordinal type name
/// — so consuming a composition never sorts. Every message the generator can name gets an
/// entry, keyed participants included: which rows an application actually runs is decided by
/// what it registered, not by this table. A message's <c>[ExcludeFromPipeline]</c> is applied
/// while this is built rather than at dispatch, and a participant the generated code could
/// not reference at all simply contributes no row.
/// </remarks>
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
