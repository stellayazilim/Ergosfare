using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Dispatch;

// Top-level and unkeyed so the generator sees the open definitions. It models them — the
// composition catalog carries Wrap<> with its handler and interceptor — but emits no plan
// for any closed form, so every dispatch fails unplanned; see the class doc.

/// <summary>Open generic command; each closed form is a message of its own.</summary>
public sealed class Wrap<T> : ICommand
{
    /// <summary>The wrapped value.</summary>
    public T? Value;

    /// <summary>The handler instance that ran, so its closed type can be inspected.</summary>
    public object? HandledBy;

    /// <summary>The pre-interceptor instance that ran, likewise.</summary>
    public object? InterceptedBy;
}

/// <inheritdoc />
public sealed class WrapHandler<T> : ICommandHandler<Wrap<T>>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Wrap<T> command, ErgosfareContext context)
    {
        command.HandledBy = this;
        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc />
public sealed class WrapPre<T> : ICommandPreInterceptor<Wrap<T>>
{
    /// <inheritdoc />
    public ValueTask<Wrap<T>> HandleAsync(Wrap<T> command, ErgosfareContext context)
    {
        command.InterceptedBy = this;
        return new ValueTask<Wrap<T>>(command);
    }
}

/// <summary>
/// Without an emitted closed plan, selecting a generic definition cannot synthesize a
/// pipeline at runtime. These fixtures pin the missing-handler guard for unsupported shapes.
/// </summary>
public sealed class GenericHandlerDispatchTests
{
    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands =>
                {
                    commands.Register(typeof(WrapHandler<>));
                    commands.Register(typeof(WrapPre<>));
                }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_closed_form_without_a_generated_descriptor_is_rejected()
    {
        await using var provider = CreateProvider();
        var command = new Wrap<int> { Value = 7 };

        var thrown = await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await provider.GetRequiredService<ICommandMediator>().SendAsync(command));

        Assert.Equal(typeof(Wrap<int>), thrown.MessageType);
        Assert.Null(command.HandledBy);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Each_unknown_instantiation_is_rejected()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        var strings = new Wrap<string> { Value = "x" };

        var thrown = await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(strings));

        Assert.Equal(typeof(Wrap<string>), thrown.MessageType);
        Assert.Null(strings.InterceptedBy);
    }
}
