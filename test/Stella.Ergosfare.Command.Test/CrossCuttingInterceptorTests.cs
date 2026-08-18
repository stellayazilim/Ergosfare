using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Command.Test.__stubs__;

namespace Stella.Ergosfare.Command.Test;

public sealed class CrossCutProbeCommand : ICommand;

public sealed class CrossCutProbeCommandHandler : ICommandHandler<CrossCutProbeCommand>
{
    public ValueTask HandleAsync(CrossCutProbeCommand message, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>
/// Pins what the cross-cutting participation idiom costs under compiled dispatch: an
/// interceptor declared over the core <see cref="IMessage"/> contract lives outside the
/// compiled closure (a discoverable one would poison every command plan in the assembly),
/// so a container that selects one holds a pipeline no compiled plan was baked against,
/// and the dispatch fails naming the divergence instead of running the interceptor.
/// </summary>
public class CrossCuttingInterceptorTests
{
    /// <summary>
    /// Registering the excluded <c>IAsyncPreInterceptor&lt;IMessage&gt;</c> (tagged with
    /// the <see cref="ICommand"/> marker) diverges the live pipeline from the compiled
    /// plan, which knows only the handler — the construct is unplanned until the generator
    /// learns to bake cross-cutting participants.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MessageScopedInterceptorWithCommandMarker_KeepsThePipelineOffThePlan()
    {
        StubCrossCuttingMessagePreInterceptor.HasCalled = false;

        var serviceProvider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<CrossCutProbeCommandHandler>()
                .Register<StubCrossCuttingMessagePreInterceptor>()))
            .BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await mediator.SendAsync(new CrossCutProbeCommand()));

        Assert.Equal(UnplannedDispatchReason.CompositionDiverged, thrown.Reason);
        Assert.False(StubCrossCuttingMessagePreInterceptor.HasCalled);
    }
}
