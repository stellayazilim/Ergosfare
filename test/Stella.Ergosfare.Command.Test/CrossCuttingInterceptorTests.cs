using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Command.Test.__stubs__;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Pins the cross-cutting participation idiom: an interceptor declared over the core
/// <see cref="Stella.Ergosfare.Core.Abstractions.IMessage"/> contract joins a module by
/// carrying that module's marker interface directly on the class — the same mechanism the
/// module-specific contracts use.
/// </summary>
public class CrossCuttingInterceptorTests
{
    /// <summary>
    /// Tests that an <c>IAsyncPreInterceptor&lt;IMessage&gt;</c> tagged with the
    /// <see cref="ICommand"/> marker runs when a concrete command is dispatched.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MessageScopedInterceptorWithCommandMarker_ShouldRunOnCommandDispatch()
    {
        StubCrossCuttingMessagePreInterceptor.HasCalled = false;

        var serviceProvider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<TestCommandHandler>()
                .Register<StubCrossCuttingMessagePreInterceptor>()))
            .BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(new TestCommand());

        Assert.True(StubCrossCuttingMessagePreInterceptor.HasCalled);
    }
}
