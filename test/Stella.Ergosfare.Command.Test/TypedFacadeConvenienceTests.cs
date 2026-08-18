using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class FacadeRoutedEcho : ICommand<string>;

[Group("facade.east")]
public sealed class FacadeEastRoutedEchoHandler : ICommandHandler<FacadeRoutedEcho, string>
{
    public ValueTask<string> HandleAsync(FacadeRoutedEcho command, ErgosfareContext context)
        => ValueTask.FromResult("east");
}

[Group("facade.west")]
public sealed class FacadeWestRoutedEchoHandler : ICommandHandler<FacadeRoutedEcho, string>
{
    public ValueTask<string> HandleAsync(FacadeRoutedEcho command, ErgosfareContext context)
        => ValueTask.FromResult("west");
}

public sealed class PlainEcho : ICommand<string>;

public sealed class PlainEchoHandler : ICommandHandler<PlainEcho, string>
{
    public ValueTask<string> HandleAsync(PlainEcho command, ErgosfareContext context)
        => ValueTask.FromResult("plain");
}

/// <summary>
/// The typed conveniences carried by <see cref="CommandMediator"/> itself, exercised through
/// a facade-typed receiver — which is the only way to reach them, since a concrete-typed call
/// site never sees the interface's default implementations.
/// </summary>
/// <remarks>
/// <para>
/// The interface's defaults forward to the untyped calls and would return the same answers,
/// so a result alone cannot tell the two apart. What these pin is that the facade's own
/// bodies route correctly: same filtering as the untyped lane, and the type pair reaching the
/// engine rather than the command's run-time type.
/// </para>
/// <para>
/// The <see cref="GroupSet"/> shapes also carry a null guard the array shape does not — a
/// missing filter object is a call-site mistake, while a null array would have been rejected
/// downstream anyway.
/// </para>
/// </remarks>
public class TypedFacadeConvenienceTests
{
    private static readonly GroupSet East = GroupSet.Of("facade.east");
    private static readonly GroupSet West = GroupSet.Of("facade.west");

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<FacadeEastRoutedEchoHandler>();
                c.Register<FacadeWestRoutedEchoHandler>();
                c.Register<PlainEchoHandler>();
            }))
            .BuildServiceProvider();

    private static CommandMediator Facade(IServiceProvider provider)
        => Assert.IsAssignableFrom<CommandMediator>(provider.GetRequiredService<ICommandMediator>());

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedSend_WithAGroupSequence_FiltersLikeTheUntypedLane()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        Assert.Equal("east",
            await mediator.SendAsync<FacadeRoutedEcho, string>(new FacadeRoutedEcho(), (IEnumerable<string>?)East, default));
        Assert.Equal("west",
            await mediator.SendAsync<FacadeRoutedEcho, string>(new FacadeRoutedEcho(), (IEnumerable<string>?)West, default));

        // No filter at all reaches the group-less lane, which is a different pipeline —
        // not the union of the grouped ones.
        Assert.Equal("plain",
            await mediator.SendAsync<PlainEcho, string>(new PlainEcho(), (IEnumerable<string>?)null, default));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedSend_ThroughTheDefaultPipeline_NeedsNoFilterArgument()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        Assert.Equal("plain", await mediator.SendAsync<PlainEcho, string>(new PlainEcho()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedSend_WithAGroupSet_FiltersAndTreatsTheEmptySetAsNoFilter()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        // Repetition is deliberate: the canonical instance is what the executor cache
        // matches on a single reference check, so the second call takes the fast path.
        Assert.Equal("east", await mediator.SendAsync<FacadeRoutedEcho, string>(new FacadeRoutedEcho(), East));
        Assert.Equal("east", await mediator.SendAsync<FacadeRoutedEcho, string>(new FacadeRoutedEcho(), East));
        Assert.Equal("west", await mediator.SendAsync<FacadeRoutedEcho, string>(new FacadeRoutedEcho(), West));

        Assert.Equal("plain", await mediator.SendAsync<PlainEcho, string>(new PlainEcho(), GroupSet.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedSend_WithAnArray_Filters()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        Assert.Equal("west", await mediator.SendAsync<FacadeRoutedEcho, string>(new FacadeRoutedEcho(), ["facade.west"]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedSend_WithANullGroupSet_IsRejectedAtTheCallSite()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        // Synchronously, before any dispatch: a missing filter object is a mistake in the
        // call, not a dispatch that produced nothing.
        var thrown = Assert.Throws<ArgumentNullException>(
            () => mediator.SendAsync<PlainEcho, string>(new PlainEcho(), (GroupSet)null!));

        Assert.Equal("groups", thrown.ParamName);
    }
}
