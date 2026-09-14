namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which adapter a staged result plan was compiled against.
/// </summary>
/// <remarks>
/// Worked out the same way the runtime binds one: the message's annotation first, if it
/// fits the result type exactly, then the framework's own carriers, then nothing.
/// </remarks>
internal enum StagedResultAdapterKind
{
    /// <summary>
    /// Nothing binds to the result type, so the plan carries no failure-reading branch at
    /// all.
    /// </summary>
    None,

    /// <summary>
    /// The framework's own carrier. The branch reads its exception field directly and a
    /// thrown failure becomes a failed carrier through the carrier's own factory, so no
    /// adapter object appears in the generated code.
    /// </summary>
    Native,

    /// <summary>
    /// A carrier bound by a <c>[ResultAdapter]</c> annotation. The plan constructs the
    /// adapter once and asks it; turning a thrown failure into a result only happens when
    /// that adapter can also build one.
    /// </summary>
    Custom,
}
