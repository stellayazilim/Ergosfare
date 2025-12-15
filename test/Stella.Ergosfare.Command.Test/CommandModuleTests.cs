using System.Reflection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Command.Test.__stubs__;

namespace Stella.Ergosfare.Command.Test;


/// <summary>
/// Contains unit tests for command module registration in Stella.Ergosfare,
/// verifying that commands are registered correctly and non-command handlers are rejected.
/// </summary>
public class CommandModuleTests
{
    /// <summary>
    /// A stub handler that is not a command handler.
    /// Used to verify that non-command handlers cannot be registered in the command module.
    /// </summary>
    private class NonCommandHandler: IHandler<IMessage, Task>
    {
        /// <summary>
        /// Handles a message asynchronously.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        /// <param name="context">The execution context.</param>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    /// <summary>
    /// Tests that a command module can register commands, including from assemblies,
    /// and that the command mediator can send commands and receive results.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRegisterCommandModule()
    {
        var serviceCollection = new ServiceCollection()

            .AddErgosfare(x => x.AddCommandModule(c =>
                c.Register<TestCommandHandler>()
                    .RegisterFromAssembly(Assembly.GetExecutingAssembly()
                    )
            )).BuildServiceProvider();
        var mediator = serviceCollection.GetRequiredService<ICommandMediator>();
        var result = mediator.SendAsync(new TestCommand());
        var stringResult = mediator.SendAsync<string>(new TestCommandStringResult());
        
        Assert.NotNull(result);
        Assert.NotNull(stringResult);
        Assert.Equal(string.Empty, await stringResult);
        
    }

    /// <summary>
    /// Tests that attempting to register a non-command handler in a command module
    /// throws a <see cref="NotSupportedException"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void  ShouldNotRegisterNonCommandsToCommandModule()
    {
        var serviceCollection = new ServiceCollection();
        Assert.Throws<NotSupportedException>(() =>
        {
            serviceCollection.AddErgosfare(x => 
                x.AddCommandModule(c =>
                c.Register(typeof(NonCommandHandler)))).BuildServiceProvider();
        });
    }
}