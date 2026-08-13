using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
///     An interceptor that takes its message as a type parameter, end to end. The generator
///     closes it over each message its constraint admits, and this is where that has to show
///     up as behaviour: the interceptor runs.
/// </summary>
/// <remarks>
///     It did not, before, in complete silence — no exception, no diagnostic, no entry in any
///     pipeline. For a validation or authorization interceptor that is a bypass, which is why
///     the assertions here are about the interceptor having run rather than about anything
///     the generator emitted.
/// </remarks>
public class OpenGenericInterceptorTests
{
    public sealed class Ship : ICommand { }

    public sealed class ShipHandler : ICommandHandler<Ship>
    {
        public ValueTask HandleAsync(Ship command, ErgosfareContext context)
        {
            context.Set("handled", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class Dock : ICommand { }

    public sealed class DockHandler : ICommandHandler<Dock>
    {
        public ValueTask HandleAsync(Dock command, ErgosfareContext context)
        {
            context.Set("handled", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class AuditCommands<TCommand> : ICommandPreInterceptor<TCommand>
        where TCommand : ICommand
    {
        public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
        {
            context.Set("audited", typeof(TCommand).Name);
            return ValueTask.FromResult(command);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GeneratedRegistration_RunsTheMonomorphizedInterceptor()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(o => o.AddCommandModule(c => c.RegisterGenerated()))
            .BuildServiceProvider();

        var context = new ErgosfareContext();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(new Ship(), context);

        Assert.True(context.Has("handled"));
        Assert.Equal(nameof(Ship), context.Get<string>("audited"));
    }

    /// <summary>
    ///     Each message gets its own instantiation — the interceptor that ran for
    ///     <see cref="Dock"/> is closed over <see cref="Dock"/>, not over whichever message
    ///     happened to dispatch first.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task EachMessage_GetsItsOwnInstantiation()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(o => o.AddCommandModule(c => c.RegisterGenerated()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var shipContext = new ErgosfareContext();
        await mediator.SendAsync(new Ship(), shipContext);

        var dockContext = new ErgosfareContext();
        await mediator.SendAsync(new Dock(), dockContext);

        Assert.Equal(nameof(Ship), shipContext.Get<string>("audited"));
        Assert.Equal(nameof(Dock), dockContext.Get<string>("audited"));
    }

    /// <summary>
    ///     Selecting the open definition selects the closed forms monomorphized from it. The
    ///     definition is the only name a caller can write, and matching selection by exact
    ///     type alone would have this registration select nothing at all.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RegisteringTheOpenDefinition_SelectsItsClosedForms()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(o => o.AddCommandModule(c =>
            {
                c.Register<ShipHandler>();
                c.Register(typeof(AuditCommands<>));
            }))
            .BuildServiceProvider();

        var context = new ErgosfareContext();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(new Ship(), context);

        Assert.True(context.Has("handled"));
        Assert.Equal(nameof(Ship), context.Get<string>("audited"));
    }

    /// <summary>
    ///     And the converse, which is what keeps selection meaningful: a container that never
    ///     registered the interceptor does not run it.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task NotRegisteringIt_LeavesItOutOfThePipeline()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(o => o.AddCommandModule(c => c.Register<ShipHandler>()))
            .BuildServiceProvider();

        var context = new ErgosfareContext();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(new Ship(), context);

        Assert.True(context.Has("handled"));
        Assert.False(context.Has("audited"));
    }
}
