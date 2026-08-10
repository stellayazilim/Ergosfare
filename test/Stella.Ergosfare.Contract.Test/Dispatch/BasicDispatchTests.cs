using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Dispatch;

/// <summary>
/// What a dispatch does when nothing but a handler is in the way: the handler runs, its
/// value comes back, and it is handed the caller's own message instance together with a
/// live execution context.
/// </summary>
public sealed class BasicDispatchTests
{
    private const string Key = "contract.dispatch";

    [DiscoveryKey(Key)]
    public sealed class Greet : ICommand
    {
        /// <summary>Set by the handler so the test can prove it ran.</summary>
        public bool Handled;

        /// <summary>The context instance the handler was handed.</summary>
        public IExecutionContext? SeenContext;
    }

    [DiscoveryKey(Key)]
    public sealed class GreetHandler : ICommandHandler<Greet>
    {
        public ValueTask HandleAsync(Greet command, IExecutionContext context)
        {
            command.Handled = true;
            command.SeenContext = context;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The instance the handler was handed, so identity can be compared.</summary>
    [DiscoveryKey(Key)]
    public sealed class Echo : ICommand<string>
    {
        public string Word = string.Empty;
    }

    [DiscoveryKey(Key)]
    public sealed class EchoHandler : ICommandHandler<Echo, string>
    {
        /// <summary>The message instance the last dispatch handed this handler.</summary>
        public static Echo? LastSeen;

        public ValueTask<string> HandleAsync(Echo command, IExecutionContext context)
        {
            LastSeen = command;
            return ValueTask.FromResult(command.Word + "!");
        }
    }

    [DiscoveryKey(Key)]
    public sealed class Sum : IQuery<int>
    {
        public int Left;
        public int Right;
    }

    [DiscoveryKey(Key)]
    public sealed class SumHandler : IQueryHandler<Sum, int>
    {
        public ValueTask<int> HandleAsync(Sum query, IExecutionContext context)
            => ValueTask.FromResult(query.Left + query.Right);
    }

    /// <summary>Never registered anywhere: the no-handler scenarios dispatch these.</summary>
    [ExcludeFromDiscovery]
    public sealed class NeverRegisteredCommand : ICommand;

    /// <inheritdoc cref="NeverRegisteredCommand"/>
    [ExcludeFromDiscovery]
    public sealed class NeverRegisteredQuery : IQuery<int>;

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated(Key))
                .AddQueryModule(queries => queries.RegisterGenerated(Key)))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_command_reaches_its_handler()
    {
        await using var provider = CreateProvider();
        var command = new Greet();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.True(command.Handled);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_handler_is_given_a_live_execution_context()
    {
        await using var provider = CreateProvider();
        var command = new Greet();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.NotNull(command.SeenContext);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_command_returns_the_value_its_handler_produced()
    {
        await using var provider = CreateProvider();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(new Echo { Word = "hello" });

        Assert.Equal("hello!", result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_handler_receives_the_callers_own_message_instance()
    {
        await using var provider = CreateProvider();
        var command = new Echo { Word = "same" };

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.Same(command, EchoHandler.LastSeen);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_query_returns_the_value_its_handler_produced()
    {
        await using var provider = CreateProvider();

        var result = await provider.GetRequiredService<IQueryMediator>()
            .QueryAsync(new Sum { Left = 20, Right = 22 });

        Assert.Equal(42, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_command_with_no_registered_handler_throws_NoHandlerFoundException()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(new NeverRegisteredCommand()));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_query_with_no_registered_handler_throws_NoHandlerFoundException()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.QueryAsync(new NeverRegisteredQuery()));
    }
}
