using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The marker the open interceptor below is constrained to. Constraining to a per-class
/// supertype rather than the module marker keeps the closed forms scoped to this class's
/// own messages — an interceptor constrained to <see cref="ICommand"/> would enter every
/// command pipeline in the assembly and keep the generator from planning any of them.
/// </summary>
public interface IAuditedCommand : ICommand;

public sealed class Ship : IAuditedCommand { }

public sealed class ShipHandler : ICommandHandler<Ship>
{
    public ValueTask HandleAsync(Ship command, ErgosfareContext context)
    {
        context.Set("handled", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class Dock : IAuditedCommand { }

public sealed class DockHandler : ICommandHandler<Dock>
{
    public ValueTask HandleAsync(Dock command, ErgosfareContext context)
    {
        context.Set("handled", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class AuditCommands<TCommand> : ICommandPreInterceptor<TCommand>
    where TCommand : IAuditedCommand
{
    public ValueTask<TCommand> HandleAsync(TCommand command, ErgosfareContext context)
    {
        context.Set("audited", typeof(TCommand).Name);
        return ValueTask.FromResult(command);
    }
}

/// <summary>
///     An interceptor that takes its message as a type parameter, end to end. The generator
///     closes it over each message its constraint admits, and this is where that has to show
///     up as behaviour: the interceptor runs — inside the compiled plan, since the closed
///     forms are baked into each admitted message's pipeline.
/// </summary>
/// <remarks>
///     It did not, before, in complete silence — no exception, no diagnostic, no entry in any
///     pipeline. For a validation or authorization interceptor that is a bypass, which is why
///     the assertions here are about the interceptor having run rather than about anything
///     the generator emitted.
/// </remarks>
public class OpenGenericInterceptorTests
{
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
    ///     And what keeps selection honest under compiled dispatch: the interceptor is part
    ///     of <see cref="Ship"/>'s compiled pipeline, so a container that never registered it
    ///     holds a pipeline the plan was not baked against — the dispatch fails naming the
    ///     divergence instead of silently running without the interceptor.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task NotRegisteringIt_DivergesFromTheCompiledPlan()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(o => o.AddCommandModule(c => c.Register<ShipHandler>()))
            .BuildServiceProvider();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
            await provider.GetRequiredService<ICommandMediator>().SendAsync(new Ship(), new ErgosfareContext()));

        Assert.Equal(UnplannedDispatchReason.CompositionDiverged, thrown.Reason);
        Assert.Equal(typeof(Ship), thrown.MessageType);
    }
}
