using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Dispatch;

/// <summary>
/// A pipeline whose participants are open generic definitions: the composition names the
/// definition, and the dispatch closes it over the runtime message's arguments before
/// resolving it from the container. The container therefore has to carry the open
/// definition as a registration — the one participant shape whose registered type and
/// resolved type are not the same.
/// </summary>
/// <remarks>
/// The generator models a generic participant definition like any other: the table keys
/// the message by its definition (<c>Wrap&lt;&gt;</c>, which every <c>Wrap&lt;T&gt;</c>
/// lookup normalizes to) and names the participant by its own unbound <c>typeof</c>, so
/// one baked entry serves every instantiation. The container carries the open definition
/// as a registration, and <c>BuildShape</c> closes it over the dispatched message's
/// arguments before it is resolved.
/// </remarks>
public sealed class GenericHandlerDispatchTests
{
    private const string Key = "contract.generic";

    [DiscoveryKey(Key)]
    public sealed class Wrap<T> : ICommand
    {
        public T? Value;

        /// <summary>The handler instance that ran, so its closed type can be inspected.</summary>
        public object? HandledBy;

        /// <summary>The pre-interceptor instance that ran, likewise.</summary>
        public object? InterceptedBy;
    }

    [DiscoveryKey(Key)]
    public sealed class WrapHandler<T> : ICommandHandler<Wrap<T>>
    {
        public ValueTask HandleAsync(Wrap<T> command, ErgosfareContext context)
        {
            command.HandledBy = this;
            return ValueTask.CompletedTask;
        }
    }

    [DiscoveryKey(Key)]
    public sealed class WrapPre<T> : ICommandPreInterceptor<Wrap<T>>
    {
        public ValueTask<Wrap<T>> HandleAsync(Wrap<T> command, ErgosfareContext context)
        {
            command.InterceptedBy = this;
            return new ValueTask<Wrap<T>>(command);
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_open_generic_handler_runs_closed_over_the_dispatched_message()
    {
        await using var provider = CreateProvider();
        var command = new Wrap<int> { Value = 7 };

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.IsType<WrapHandler<int>>(command.HandledBy);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_open_generic_interceptor_runs_closed_over_the_dispatched_message()
    {
        await using var provider = CreateProvider();
        var command = new Wrap<string> { Value = "x" };

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.IsType<WrapPre<string>>(command.InterceptedBy);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_same_definition_serves_every_instantiation()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var ints = new Wrap<int>();
        var strings = new Wrap<string>();

        await mediator.SendAsync(ints);
        await mediator.SendAsync(strings);

        Assert.IsType<WrapHandler<int>>(ints.HandledBy);
        Assert.IsType<WrapHandler<string>>(strings.HandledBy);
    }
}
