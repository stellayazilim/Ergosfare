using System.Reflection;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Messaging.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Commands.Test;

public class CommandModuleTests
{

    public record TestCommand : ICommand<string>;

    public sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
    {
        public Task<string> HandleAsync(TestCommand message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("hello");
        }
    }
    
    [Fact]
    public async Task Send_CreateProductCommand_ShouldGoThroughHandlersCorrectly()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddErgosfare(configuration => { configuration.AddCommandModule(builder => { builder.RegisterFromAssembly(Assembly.GetExecutingAssembly()); }); })
            .BuildServiceProvider();

        var commandMediator = serviceProvider.GetRequiredService<ICommandMediator>();
 
        // Act
        var result = await commandMediator.SendAsync(new TestCommand {});

        Assert.NotNull(result);
        Assert.Equal("hello", result);
    }

}