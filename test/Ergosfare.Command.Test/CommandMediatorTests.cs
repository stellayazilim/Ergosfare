using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Core;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Ergosfare.Command.Test;

/// <summary>
/// Contains unit tests for the <see cref="CommandMediator"/> class,
/// verifying that commands can be resolved and executed correctly.
/// </summary>
public class CommandMediatorTests
{
    /// <summary>
    /// Tests that <see cref="CommandMediator"/> can resolve and send a command
    /// of type <see cref="StubNonGenericCommand"/> without returning a result.
    /// </summary>
    /// <remarks>
    /// This test ensures that the command mediator can instantiate the correct handler
    /// and that sending a command does not return null.
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldResolveCommandTCommand()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCoreModule(x =>
                {
                    x.Register<StubNonGenericCommandHandler>();
                });
            }).BuildServiceProvider();

        var messageMediator = serviceCollection.GetService<IMessageMediator>();
        var mediator = new CommandMediator(
            new SignalHub(),
            serviceCollection.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            new ResultAdapterService(),
            messageMediator!);

        var result = mediator.SendAsync(new StubNonGenericCommand(), null, CancellationToken.None);

        Assert.NotNull(result);
    }

    /// <summary>
    /// Tests that <see cref="CommandMediator"/> can resolve and send a command
    /// of type <see cref="StubNonGenericCommandStringResult"/> and return the expected result.
    /// </summary>
    /// <remarks>
    /// This test verifies that the command mediator correctly invokes the handler
    /// for a command returning a result, and that the returned value matches expectations.
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldResolveCommandTCommandTResult()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCoreModule(x =>
                {
                    x.Register<StubNonGenericCommandStringResultHandler>();
                });
            }).BuildServiceProvider();

        var messageMediator = serviceCollection.GetRequiredService<IMessageMediator>();
        var mediator = new CommandMediator(
            new SignalHub(),
            serviceCollection.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            new ResultAdapterService(),
            messageMediator!);

        var result = mediator.SendAsync(new StubNonGenericCommandStringResult(), StubDefaultMediationSetting.CommandDefaultSetting, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, await result);
    }
}
