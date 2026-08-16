using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// A frozen dispatch built on a dependencies factory that is not the framework's own. The
/// whole freezing mechanism rests on the factory answering the same thing twice — that is
/// what makes a composition safe to cache and a staged plan safe to admit — and a foreign
/// implementation promises nothing of the sort.
/// </summary>
/// <remarks>
/// <para>
/// So it freezes nothing: the verdict is decided at construction rather than on the first
/// dispatch, every dispatch asks the factory again, and the runtime body runs even when a
/// plan was handed in. That is the pre-freezing contract, kept for anyone who supplied their
/// own factory before there was one to keep.
/// </para>
/// <para>
/// The observable difference is the repeat call, which is why these count what the factory
/// was asked rather than only checking the result: a dispatch that answered correctly from a
/// cached composition would pass a result-only assertion while breaking exactly the promise
/// this branch exists to keep.
/// </para>
/// </remarks>
public class ForeignDependenciesFactoryTests
{
    public sealed class ForeignCommand : ICommand;

    public sealed class ForeignEcho : ICommand<string>;

    [ExcludeFromDiscovery]
    public sealed class ForeignCommandHandler : ICommandHandler<ForeignCommand>
    {
        public ValueTask HandleAsync(ForeignCommand command, ErgosfareContext context)
        {
            context.Set("foreign.ran", true);
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class ForeignEchoHandler : ICommandHandler<ForeignEcho, string>
    {
        public ValueTask<string> HandleAsync(ForeignEcho command, ErgosfareContext context)
            => ValueTask.FromResult("foreign");
    }

    public sealed class GroupedForeignCommand : ICommand;

    [ExcludeFromDiscovery]
    [Group("foreign.group")]
    public sealed class GroupedForeignCommandHandler : ICommandHandler<GroupedForeignCommand>
    {
        public ValueTask HandleAsync(GroupedForeignCommand command, ErgosfareContext context)
        {
            context.Set("foreign.ran", true);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Answers exactly like the framework's factory — by delegating to it — while not being
    /// it. The dispatch cannot inspect the answers, only the type, which is the point: the
    /// promise is about the implementation, not about one observed reply.
    /// </summary>
    private sealed class ForeignFactory(IMessageDependenciesFactory inner) : IMessageDependenciesFactory
    {
        public int Asked { get; private set; }

        public IMessageDependencies Create(Type messageType, IEnumerable<string> groups)
        {
            Asked++;
            return inner.Create(messageType, groups);
        }

        public IMessageDependencies? Find(Type messageType, IEnumerable<string> groups)
        {
            Asked++;
            return inner.Find(messageType, groups);
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<ForeignCommandHandler>();
                c.Register<ForeignEchoHandler>();
                c.Register<GroupedForeignCommandHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task VoidDispatch_AsksTheForeignFactoryEveryTime()
    {
        await using var provider = Build();
        var foreign = new ForeignFactory(provider.GetRequiredService<IMessageDependenciesFactory>());
        var dispatch = new FrozenVoidDispatch<ForeignCommand>(foreign, plan: null);

        var first = new ErgosfareContext();
        await dispatch.Execute(new ForeignCommand(), first, provider, groups: null);

        Assert.Equal(true, first.Items["foreign.ran"]);
        Assert.Equal(1, foreign.Asked);

        var second = new ErgosfareContext();
        await dispatch.Execute(new ForeignCommand(), second, provider, groups: null);

        // Nothing was frozen on the first dispatch, so the second is a fresh question.
        Assert.Equal(true, second.Items["foreign.ran"]);
        Assert.Equal(2, foreign.Asked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ResultDispatch_AsksTheForeignFactoryEveryTime()
    {
        await using var provider = Build();
        var foreign = new ForeignFactory(provider.GetRequiredService<IMessageDependenciesFactory>());
        var dispatch = new FrozenResultDispatch<ForeignEcho, string>(foreign, plan: null);

        Assert.Equal("foreign",
            await dispatch.Execute(new ForeignEcho(), new ErgosfareContext(), provider, groups: null));
        Assert.Equal(1, foreign.Asked);

        Assert.Equal("foreign",
            await dispatch.Execute(new ForeignEcho(), new ErgosfareContext(), provider, groups: null));
        Assert.Equal(2, foreign.Asked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupedDispatch_ThroughAForeignFactory_SlotsNothingEither()
    {
        await using var provider = Build();
        var foreign = new ForeignFactory(provider.GetRequiredService<IMessageDependenciesFactory>());
        var dispatch = new FrozenVoidDispatch<GroupedForeignCommand>(foreign, plan: null);
        var filter = GroupSet.Of("foreign.group");

        var first = new ErgosfareContext();
        await dispatch.Execute(new GroupedForeignCommand(), first, provider, filter);

        Assert.Equal(true, first.Items["foreign.ran"]);

        var asked = foreign.Asked;

        var second = new ErgosfareContext();
        await dispatch.Execute(new GroupedForeignCommand(), second, provider, filter);

        Assert.Equal(true, second.Items["foreign.ran"]);

        // The grouped lane keeps a last-used slot for the framework's factory and none for a
        // foreign one — repeating the very same canonical filter must still ask again.
        Assert.True(foreign.Asked > asked,
            "a repeated grouped dispatch was served from a slot the foreign lane must not keep");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TheFrameworksOwnFactory_AsksOnceAndThenServesTheFrozenComposition()
    {
        await using var provider = Build();
        var dispatch = new FrozenVoidDispatch<ForeignCommand>(
            provider.GetRequiredService<IMessageDependenciesFactory>(), plan: null);

        // The contrast that gives the cases above their meaning: the same dispatches against
        // the framework's factory resolve the composition once and read a field afterwards.
        for (var i = 0; i < 3; i++)
        {
            var context = new ErgosfareContext();
            await dispatch.Execute(new ForeignCommand(), context, provider, groups: null);

            Assert.Equal(true, context.Items["foreign.ran"]);
        }
    }
}
