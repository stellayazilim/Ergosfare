using Stella.Ergosfare.Core.Abstractions.Planning;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The catalog is the seam between a process-wide table and a per-container pipeline: the
/// generator emits every participant it can name, and what an application actually runs is
/// what it registered. These pin the projection — an unselective catalog serves the table
/// untouched, a selective one serves exactly its own registrations, and narrowing is a
/// filter, never a reordering.
/// </summary>
public class DispatchPlanCatalogTests
{
    [Fact]
    public void InitializedCatalog_RejectsRegistrationAndDescriptorMutation()
    {
        var catalog = new DispatchPlanCatalog();
        catalog.Select(typeof(SelectedHandler));
        catalog.Seal();
        Assert.Throws<InvalidOperationException>(() => catalog.Select(typeof(SelectedPre)));
        Assert.Throws<InvalidOperationException>(() => catalog.Select(new[] { typeof(SelectedPre) }));
        Assert.Throws<InvalidOperationException>(() => catalog.Add(Composition));
        Assert.Equal(new[] { typeof(SelectedHandler) }, catalog.Selections);
    }

    private sealed record CatalogMessage;

    private sealed record UnknownMessage;

    private sealed class SelectedHandler;

    private sealed class SelectedPre;

    // Stands in for a participant belonging to a discovery key this container did not
    // register: compiled into the table, absent from the application.
    private sealed class ForeignPre;

    /// <summary>
    /// The table entry, seeded once. The process-wide store admits the first instance for
    /// a message type and ignores later ones, so every test here has to mean the same
    /// object — which is also the identity the unselective projection is pinned against.
    /// </summary>
    private static readonly PipelineDescriptor Composition = BuildComposition();

    private static PipelineDescriptor BuildComposition()
        => new(
            typeof(CatalogMessage),
            [new FrozenParticipant(typeof(SelectedHandler))],
            [],
            // Ordinal name order: ForeignPre before SelectedPre.
            [new FrozenParticipant(typeof(ForeignPre)), new FrozenParticipant(typeof(SelectedPre))],
            [], [], [], [], [], [], []);

    [Fact]
    public void SelectionIncludesOnlyRegisteredParticipants()
    {
        GeneratedPlanRegistry.AddPipelineDescriptor(Composition);
        GeneratedPlanRegistry.AddParticipant<SelectedHandler>();
        GeneratedPlanRegistry.AddParticipant<SelectedPre>();
        var catalog = new DispatchPlanCatalog();
        Assert.Empty(catalog.SelectedParticipants());
        catalog.Select(typeof(SelectedHandler));
        catalog.Select(typeof(SelectedPre));
        Assert.Equal(new[] { typeof(SelectedHandler), typeof(SelectedPre) }.OrderBy(t => t.Name),
            catalog.SelectedParticipants().OrderBy(t => t.Name));
    }
}
