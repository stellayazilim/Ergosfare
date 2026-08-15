using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test;

/// <summary>
/// Each module facade resolves under both names — its interface and its concrete class.
/// </summary>
/// <remarks>
/// The two spellings are not a convenience. A dispatch through the interface goes through a
/// generic virtual method the JIT cannot devirtualize; the same call on the class is direct.
/// It is a few nanoseconds — nothing for most callers, everything for a hot loop — so the
/// choice belongs to the caller, which means both names have to resolve.
/// </remarks>
public class ConcreteFacadeResolutionTests
{
    private static ServiceProvider BuildProvider()
        => new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(_ => { });
                options.AddQueryModule(_ => { });
                options.AddEventModule(_ => { });
            })
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    public void EveryModuleFacade_ResolvesUnderBothNames()
    {
        using var provider = BuildProvider();

        // The interface, as an application has always injected it.
        Assert.NotNull(provider.GetRequiredService<ICommandMediator>());
        Assert.NotNull(provider.GetRequiredService<IQueryMediator>());
        Assert.NotNull(provider.GetRequiredService<IEventMediator>());

        // And the concrete class, which dispatches without the generic-virtual indirection.
        Assert.NotNull(provider.GetRequiredService<CommandMediator>());
        Assert.NotNull(provider.GetRequiredService<QueryMediator>());
        Assert.NotNull(provider.GetRequiredService<EventMediator>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ConcreteResolution_ServesTheRegisteredFacade()
    {
        using var provider = BuildProvider();

        // One object graph behind two names: the concrete registration projects the
        // interface's own registration rather than building a second facade, so replacing
        // the interface registration replaces both.
        Assert.IsAssignableFrom<CommandMediator>(provider.GetRequiredService<ICommandMediator>());
        Assert.IsAssignableFrom<QueryMediator>(provider.GetRequiredService<IQueryMediator>());
        Assert.IsAssignableFrom<EventMediator>(provider.GetRequiredService<IEventMediator>());
    }
}
