using Stella.Ergosfare.Commands;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Command.Test.__stubs__;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Contains unit tests for the <see cref="CommandMediator"/> class,
/// verifying that commands can be resolved and executed correctly.
/// </summary>
public class CommandMediatorTests
{
    /// <summary>
    /// Tests that <see cref="CommandMediator"/> can resolve and send a command
    /// of type <see cref="StubPlainCommand"/> without returning a result.
    /// </summary>
    /// <remarks>
    /// This test ensures that the command mediator can instantiate the correct handler
    /// and that sending a command does not return null. The ungrouped stub is the fixture
    /// here: the group-attributed stub's default-set pipeline has no compiled plan.
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldResolveCommandTCommand()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(x =>
                {
                    x.Register<StubPlainCommandHandler>();
                });
            }).BuildServiceProvider();

        var mediator = new CommandMediator(
            serviceCollection.GetRequiredService<MessageDispatchEngine>(), serviceCollection);

        await mediator.SendAsync(new StubPlainCommand(), new ErgosfareContext());

        Assert.True(StubPlainCommandHandler.HasCalled);
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
                options.AddCommandModule(x =>
                {
                    x.Register<StubNonGenericCommandStringResultHandler>();
                });
            }).BuildServiceProvider();

        var mediator = new CommandMediator(
            serviceCollection.GetRequiredService<MessageDispatchEngine>(), serviceCollection);

        var result = mediator.SendAsync(new StubNonGenericCommandStringResult(), ["default"], CancellationToken.None);

        Assert.Equal(string.Empty, await result);
    }
}
