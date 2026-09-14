using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Command.Test.__stubs__;

namespace Stella.Ergosfare.Command.Test;

public sealed class CrossCutProbeCommand : ICommand;

[Group("cross-cutting-probe")]
public sealed class CrossCutProbeCommandHandler : ICommandHandler<CrossCutProbeCommand>
{
    public ValueTask HandleAsync(CrossCutProbeCommand message, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>Explicit selection includes excluded cross-cutting participants in generated plans.</summary>
public class CrossCuttingInterceptorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ExplicitlySelectedCrossCuttingInterceptor_RunsInTheGeneratedPlan()
    {
        StubCrossCuttingMessagePreInterceptor.HasCalled = false;

        await using var serviceProvider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<CrossCutProbeCommandHandler>()
                .Register<StubCrossCuttingMessagePreInterceptor>()))
            .BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(new CrossCutProbeCommand(), "cross-cutting-probe", CancellationToken.None);
        Assert.True(StubCrossCuttingMessagePreInterceptor.HasCalled);
    }
}
