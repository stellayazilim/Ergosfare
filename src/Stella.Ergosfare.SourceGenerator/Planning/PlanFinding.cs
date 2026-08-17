using System.Collections.Immutable;
using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator.Planning;

/// <summary>
/// Something the planner saw that the build should be told about.
/// </summary>
/// <param name="Kind">What was seen.</param>
/// <param name="Message">The message whose plan it concerns.</param>
/// <param name="Groups">The group set it was judged under, empty for the default one.</param>
/// <param name="First">The first participant involved.</param>
/// <param name="Second">The second participant, where the finding is about a pair.</param>
/// <param name="Location">Where to report it.</param>
/// <remarks>
/// <para>
/// The planner has no <c>SourceProductionContext</c>, and its vocabulary for a shape it
/// cannot plan has always been a bare <c>return</c>. That is why a lost plan never appeared
/// in a build: not because the reason was unknown at the point it was decided — everything
/// needed is in scope there — but because there was nowhere to put it.
/// </para>
/// <para>
/// This is that missing channel. A disqualification that a dispatch cannot survive becomes a
/// finding, the pipeline reports it, and the build says what the dispatch would have done at
/// run time instead of quietly taking a slower path there.
/// </para>
/// </remarks>
internal readonly record struct PlanFinding(
    PlanFindingKind Kind,
    string Message,
    ImmutableArray<string> Groups,
    string First,
    string Second,
    LocationInfo? Location);

/// <summary>
/// What a <see cref="PlanFinding"/> saw.
/// </summary>
internal enum PlanFindingKind
{
    /// <summary>
    /// A group set selects two main handlers for one message, so every send under that set
    /// throws.
    /// </summary>
    ContestedInGroupSet = 0,
}
