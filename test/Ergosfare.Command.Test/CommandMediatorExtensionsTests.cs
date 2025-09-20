using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ergosfare.Command.Test;

public class CommandMediatorExtensionsTests
{
    
    /// <summary>
    /// Tests that the <see cref="ICommandMediator"/> executes a command with a specific group
    /// and only invokes the interceptors associated with that group.
    /// </summary>
    /// <remarks>
    /// This test uses mock pre-interceptors and a stub command to verify group-based execution behavior.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithGroup()
    {
        var mockInterceptor1 = new Mock<StubCommandPreInterceptor1>();
        var mockInterceptor2 = new Mock<StubCommandPreInterceptor2>();

        var cancellationToken = CancellationToken.None;
        #pragma warning disable CS0618 // Type or member is obsolete
        var cmd = new StubNonGenericCommand();
        #pragma warning restore CS0618 // Type or member is obsolete

        mockInterceptor1.Setup(s =>
        #pragma warning disable CS0618 // Type or member is obsolete
                s.HandleAsync(It.IsAny<StubNonGenericCommand>(), It.IsAny<IExecutionContext>()))
        #pragma warning restore CS0618 // Type or member is obsolete
            .CallBase();
        mockInterceptor2.Setup(s =>
            s.HandleAsync(It.IsAny<StubNonGenericCommand>(), It.IsAny<IExecutionContext>()))
            .CallBase();

        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register(mockInterceptor2.Object.GetType());
                    module.Register(mockInterceptor1.Object.GetType());
                    module.Register<StubNonGenericCommand>();
                });
            }).BuildServiceProvider();

        var mediator = services.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(cmd, new[] { "group2" }, cancellationToken);

        Assert.True(StubNonGenericCommandHandler.HasCalled);
        Assert.False(StubCommandPreInterceptor1.HasCalled);
        Assert.True(StubCommandPreInterceptor2.HasCalled);
    }

    /// <summary>
    /// Tests that the <see cref="ICommandMediator"/> executes a command
    /// and supports passing a <see cref="CancellationToken"/>.
    /// </summary>
    /// <remarks>
    /// This test ensures that a command handler is invoked when using the cancellation token overload.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithCancellationToken()
    {
        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommand();

        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register<StubNonGenericCommandHandler>();
                });
            }).BuildServiceProvider();

        var mediator = services.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(cmd, cancellationToken);

        Assert.True(StubNonGenericCommandHandler.HasCalled);
    }

    /// <summary>
    /// Tests that the <see cref="ICommandMediator"/> executes a command with a result type
    /// and supports a <see cref="CancellationToken"/>.
    /// </summary>
    /// <remarks>
    /// This test verifies that the handler for a command returning a result is invoked
    /// and produces the expected result.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithResultCancellationToken()
    {
        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommandStringResult();

        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register<StubNonGenericCommandStringResultHandler>();
                });
            }).BuildServiceProvider();

        var mediator = services.GetRequiredService<ICommandMediator>();

        var result = await mediator.SendAsync(cmd, cancellationToken);

        Assert.True(StubNonGenericCommandStringResultHandler.HasCalled);
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that the <see cref="ICommandMediator"/> executes a command with a result type
    /// and a specific group tag.
    /// </summary>
    /// <remarks>
    /// This test ensures that commands returning a result respect group filtering
    /// and the correct handler is invoked.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithResultGroup()
    {
        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommandStringResult();

        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register<StubNonGenericCommandStringResultHandler>();
                });
            }).BuildServiceProvider();

        var mediator = services.GetRequiredService<ICommandMediator>();

        var result = await mediator.SendAsync(cmd, new[] { "default" }, cancellationToken);

        Assert.True(StubNonGenericCommandStringResultHandler.HasCalled);
        Assert.Equal(string.Empty, result);
    }

}