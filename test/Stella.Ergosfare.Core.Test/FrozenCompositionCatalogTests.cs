using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The catalog is the seam between a process-wide table and a per-container pipeline: the
/// generator emits every participant it can name, and what an application actually runs is
/// what it registered. These pin the projection — an unselective catalog serves the table
/// untouched, a selective one serves exactly its own registrations, and narrowing is a
/// filter, never a reordering.
/// </summary>
public class FrozenCompositionCatalogTests
{
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
    private static readonly FrozenComposition Composition = BuildComposition();

    private static FrozenComposition BuildComposition()
        => new(
            typeof(CatalogMessage),
            [new FrozenParticipant(typeof(SelectedHandler))],
            [],
            // Ordinal name order: ForeignPre before SelectedPre.
            [new FrozenParticipant(typeof(ForeignPre)), new FrozenParticipant(typeof(SelectedPre))],
            [], [], [], [], [], [], []);

    /// <summary>
    /// Selection is the whole answer, including when it is empty: a container that
    /// registered nothing runs nothing. Serving the unfiltered table instead would hand an
    /// application every participant the closure happens to compile — including ones it
    /// cannot resolve.
    /// </summary>
    [Fact]
    public void ACatalogToldNothing_ServesAnEmptyPipeline()
    {
        GeneratedDispatchRoots.AddFrozenComposition(Composition);

        var projected = Assert.IsType<FrozenComposition>(new FrozenCompositionCatalog().Find(typeof(CatalogMessage)));

        Assert.Empty(projected.Handlers);
        Assert.Empty(projected.PreInterceptors);
    }

    /// <summary>
    /// The unnarrowed case: a container that registered the whole table gets the entry
    /// itself back, not a rebuilt copy.
    /// </summary>
    [Fact]
    public void ACatalogThatRegisteredEverything_ServesTheTableUntouched()
    {
        GeneratedDispatchRoots.AddFrozenComposition(Composition);

        var catalog = new FrozenCompositionCatalog();
        catalog.Select(new[] { typeof(SelectedHandler), typeof(SelectedPre), typeof(ForeignPre) });

        Assert.Same(Composition, catalog.Find(typeof(CatalogMessage)));

        var shape = Composition.BuildShape(typeof(CatalogMessage), []);

        Assert.Equal([typeof(SelectedHandler)], shape.Handlers);
        Assert.Equal([typeof(ForeignPre), typeof(SelectedPre)], shape.PreInterceptors);
    }

    [Fact]
    public void ASelectiveCatalog_ServesOnlyTheRegisteredRows()
    {
        GeneratedDispatchRoots.AddFrozenComposition(Composition);

        var catalog = new FrozenCompositionCatalog();
        catalog.Select(new[] { typeof(SelectedHandler), typeof(SelectedPre) });

        var projected = Assert.IsType<FrozenComposition>(catalog.Find(typeof(CatalogMessage)));

        Assert.Equal(typeof(SelectedPre), Assert.Single(projected.PreInterceptors).HandlerType);
        Assert.Equal(typeof(SelectedHandler), Assert.Single(projected.Handlers).HandlerType);

        // And the narrowed table derives the same pipeline minus the unregistered row —
        // the selection is a filter, never a reordering.
        var shape = projected.BuildShape(typeof(CatalogMessage), []);

        Assert.Equal([typeof(SelectedHandler)], shape.Handlers);
        Assert.Equal([typeof(SelectedPre)], shape.PreInterceptors);
    }

    [Fact]
    public void ProjectionIsStablePerMessageType()
    {
        GeneratedDispatchRoots.AddFrozenComposition(Composition);

        var catalog = new FrozenCompositionCatalog();
        catalog.Select(typeof(SelectedHandler));

        Assert.Same(catalog.Find(typeof(CatalogMessage)), catalog.Find(typeof(CatalogMessage)));
    }

    [Fact]
    public void AMessageWithNoTableEntry_ResolvesToNothing()
        => Assert.Null(new FrozenCompositionCatalog().Find(typeof(UnknownMessage)));
}
