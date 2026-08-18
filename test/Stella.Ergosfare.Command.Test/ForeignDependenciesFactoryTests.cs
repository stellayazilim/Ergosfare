using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// A frozen dispatch built on a dependencies factory that is not the framework's own. The
/// whole plan gate rests on the factory answering the same thing twice — that is what makes
/// a composition verifiable against a compiled plan — and a foreign implementation promises
/// nothing of the sort.
/// </summary>
/// <remarks>
/// <para>
/// So nothing it produces can be verified, and nothing is dispatched at run time that was
/// not produced at compile time: every dispatch under a foreign factory fails with
/// <see cref="UnplannedDispatchException"/> naming
/// <see cref="UnplannedDispatchReason.ForeignDependenciesFactory"/>, before the factory is
/// ever asked for participants. The pre-freezing contract — ask again on every dispatch —
/// is gone with the runtime lane that kept it.
/// </para>
/// <para>
/// The fixtures stay excluded from discovery on purpose: these tests construct the frozen
/// executors directly, planless, and pin the failures of that shape.
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
    public async Task VoidDispatch_UnderAForeignFactory_FailsEveryDispatch()
    {
        await using var provider = Build();
        var foreign = new ForeignFactory(provider.GetRequiredService<IMessageDependenciesFactory>());
        var dispatch = new FrozenVoidDispatch<ForeignCommand>(foreign, plan: null);

        // The failure is decided by the factory's type alone, before participants are ever
        // asked for — and it is decided again on every dispatch, since a failed
        // verification caches nothing.
        for (var i = 0; i < 2; i++)
        {
            var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
                await dispatch.Execute(new ForeignCommand(), new ErgosfareContext(), provider, groups: null));

            Assert.Equal(UnplannedDispatchReason.ForeignDependenciesFactory, thrown.Reason);
            Assert.Equal(typeof(ForeignCommand), thrown.MessageType);
        }

        Assert.Equal(0, foreign.Asked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ResultDispatch_UnderAForeignFactory_FailsEveryDispatch()
    {
        await using var provider = Build();
        var foreign = new ForeignFactory(provider.GetRequiredService<IMessageDependenciesFactory>());
        var dispatch = new FrozenResultDispatch<ForeignEcho, string>(foreign, plan: null);

        for (var i = 0; i < 2; i++)
        {
            var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
                await dispatch.Execute(new ForeignEcho(), new ErgosfareContext(), provider, groups: null));

            Assert.Equal(UnplannedDispatchReason.ForeignDependenciesFactory, thrown.Reason);
        }

        Assert.Equal(0, foreign.Asked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupedDispatch_ThroughAForeignFactory_FailsTheSameWay()
    {
        await using var provider = Build();
        var foreign = new ForeignFactory(provider.GetRequiredService<IMessageDependenciesFactory>());
        var dispatch = new FrozenVoidDispatch<GroupedForeignCommand>(foreign, plan: null);
        var filter = GroupSet.Of("foreign.group");

        // The grouped lane verifies per set, and a foreign factory fails that verification
        // for every set it is asked about.
        for (var i = 0; i < 2; i++)
        {
            var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
                await dispatch.Execute(new GroupedForeignCommand(), new ErgosfareContext(), provider, filter));

            Assert.Equal(UnplannedDispatchReason.ForeignDependenciesFactory, thrown.Reason);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TheFrameworksOwnFactory_StillRefusesAPlanlessDispatch()
    {
        await using var provider = Build();
        var dispatch = new FrozenVoidDispatch<ForeignCommand>(
            provider.GetRequiredService<IMessageDependenciesFactory>(), plan: null);

        // The contrast that gives the cases above their meaning: the framework's own factory
        // is verifiable, but a dispatch without a compiled plan still has nothing to run —
        // the excluded handler kept the generator from baking one, so the failure names the
        // missing plan rather than the factory. Nothing is cached: every dispatch re-derives
        // the same answer.
        for (var i = 0; i < 3; i++)
        {
            var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
                await dispatch.Execute(new ForeignCommand(), new ErgosfareContext(), provider, groups: null));

            Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        }
    }
}
