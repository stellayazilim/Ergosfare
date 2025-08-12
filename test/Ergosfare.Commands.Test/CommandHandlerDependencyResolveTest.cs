using System.Reflection;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;


namespace Ergosfare.Commands.Test;

public class CommandHandlerDependencyResolveTest
{

    public class HelloService
    {
        public string Hello { get; } = "Hello";
    }
    public record TestCommand : ICommand<string>;

    public sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
    {
        private readonly HelloService _helloService;

        public TestCommandHandler(HelloService helloService)
        {
            _helloService = helloService;
        }
        public Task<string> HandleAsync(TestCommand message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_helloService.Hello);
        }
    }
    
    [Fact]
    public async Task CommandHandlerShouldResolveDependencies()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddScoped<HelloService>()
            .AddErgosfare(configuration => { configuration.AddCommandModule(builder => { builder.RegisterFromAssembly(Assembly.GetExecutingAssembly()); }); })
            .BuildServiceProvider();
        
        var commandMediator = serviceProvider.GetRequiredService<ICommandMediator>();
 
        // Act
        var result = await commandMediator.SendAsync(new TestCommand {});

        Assert.NotNull(result);
        Assert.Equal(new HelloService().Hello, result);

        await serviceProvider.DisposeAsync();

    }
}