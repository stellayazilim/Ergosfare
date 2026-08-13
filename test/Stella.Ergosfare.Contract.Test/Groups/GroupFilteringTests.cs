using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Groups;

/// <summary>
/// Group filtering is exclusive in both directions: a default dispatch sees only
/// ungrouped participants, and a filtered dispatch sees only participants carrying one of
/// the requested groups.
/// </summary>
public sealed class GroupFilteringTests
{
    private const string Key = "contract.groups";
    private const string Reporting = "reporting";

    /// <summary>The canonical filter form, interned and reused as the API intends.</summary>
    private static readonly GroupSet ReportingSet = GroupSet.Of(Reporting);

    [DiscoveryKey(Key)]
    public sealed class Mixed : ICommand;

    [DiscoveryKey(Key)]
    public sealed class MixedHandler : ICommandHandler<Mixed>
    {
        public ValueTask HandleAsync(Mixed command, ErgosfareContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Only a filtered dispatch may see this pre-interceptor.</summary>
    [DiscoveryKey(Key)]
    [Group(Reporting)]
    public sealed class MixedReportingPre : ICommandPreInterceptor<Mixed>
    {
        public ValueTask<Mixed> HandleAsync(Mixed command, ErgosfareContext context)
        {
            context.Mark("pre:reporting");
            return ValueTask.FromResult(command);
        }
    }

    /// <summary>A command whose only handler is grouped.</summary>
    [DiscoveryKey(Key)]
    [Group(Reporting)]
    public sealed class ReportingOnly : ICommand
    {
        /// <summary>Set by the handler — the only observation the settings-less overloads leave.</summary>
        public bool Handled;
    }

    [DiscoveryKey(Key)]
    [Group(Reporting)]
    public sealed class ReportingOnlyHandler : ICommandHandler<ReportingOnly>
    {
        public ValueTask HandleAsync(ReportingOnly command, ErgosfareContext context)
        {
            command.Handled = true;
            context.Mark("handler:reporting");
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_default_dispatch_skips_grouped_participants()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(new Mixed(), recorder.Commands());

        recorder.AssertStages("handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_grouped_handler_is_unreachable_from_a_default_dispatch()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // A registered message whose handlers are all filtered out reports the same type
        // as a message that was never registered at all; only the message differs.
        var thrown = await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(new ReportingOnly()));

        Assert.Contains(nameof(ReportingOnly), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_grouped_dispatch_reaches_the_grouped_handler()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(new ReportingOnly(), recorder.Commands(), [Reporting]);

        recorder.AssertStages("handler:reporting");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_grouped_dispatch_excludes_the_ungrouped_handler()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(
                new Mixed(), new[] { Reporting }, CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_plain_group_sequence_and_a_canonical_GroupSet_select_the_same_participants()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var viaSequence = new PipelineRecorder();
        await mediator.SendAsync(new ReportingOnly(), viaSequence.Commands(), new[] { Reporting });

        var viaGroupSet = new PipelineRecorder();
        await mediator.SendAsync(new ReportingOnly(), viaGroupSet.Commands(), ReportingSet);

        Assert.Equal(viaSequence.Stages, viaGroupSet.Stages);
        viaGroupSet.AssertStages("handler:reporting");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_GroupSet_overload_dispatches_the_grouped_pipeline()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        var command = new ReportingOnly();

        await mediator.SendAsync(command, ReportingSet);

        Assert.True(command.Handled);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_empty_GroupSet_dispatches_the_default_pipeline()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(new ReportingOnly(), GroupSet.Empty));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_string_array_extension_selects_the_grouped_pipeline_too()
    {
        await using var provider = CreateProvider();
        var command = new ReportingOnly();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command, new[] { Reporting });

        Assert.True(command.Handled);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_filtered_out_dispatch_is_still_catchable_as_an_InvalidOperationException()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // This case threw a plain InvalidOperationException before the two "nothing will
        // handle this" exceptions were unified. NoHandlerFoundException derives from it so
        // that callers who were catching that keep catching it — the compatibility half of
        // the unification, which nothing else here would notice losing.
        var thrown = await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(new ReportingOnly()));

        Assert.IsAssignableFrom<InvalidOperationException>(thrown);
        Assert.Equal(typeof(ReportingOnly), thrown.MessageType);
    }
}
