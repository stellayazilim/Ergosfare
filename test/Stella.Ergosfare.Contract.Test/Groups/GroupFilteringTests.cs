using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Groups;

// Top-level and unkeyed so the generator bakes the plans — the default-set plan and the
// group-filtered ones alike; every type stays scoped to this area.

/// <summary>Command with an ungrouped handler and a grouped pre-interceptor.</summary>
public sealed class MixedAudience : ICommand;

/// <inheritdoc />
public sealed class MixedAudienceHandler : ICommandHandler<MixedAudience>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(MixedAudience command, ErgosfareContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Only a filtered dispatch may see this pre-interceptor.</summary>
[Group(GroupFilteringTests.Reporting)]
public sealed class MixedAudienceReportingPre : ICommandPreInterceptor<MixedAudience>
{
    /// <inheritdoc />
    public ValueTask<MixedAudience> HandleAsync(MixedAudience command, ErgosfareContext context)
    {
        context.Mark("pre:reporting");
        return ValueTask.FromResult(command);
    }
}

/// <summary>A command whose only handler is grouped.</summary>
[Group(GroupFilteringTests.Reporting)]
public sealed class ReportingOnly : ICommand
{
    /// <summary>Set by the handler — the only observation the settings-less overloads leave.</summary>
    public bool Handled;
}

/// <inheritdoc />
[Group(GroupFilteringTests.Reporting)]
public sealed class ReportingOnlyHandler : ICommandHandler<ReportingOnly>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ReportingOnly command, ErgosfareContext context)
    {
        command.Handled = true;
        context.Mark("handler:reporting");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Group filtering is exclusive in both directions: a default dispatch sees only
/// ungrouped participants, and a filtered dispatch sees only participants carrying one of
/// the requested groups.
/// </summary>
public sealed class GroupFilteringTests
{
    /// <summary>The group name this area filters on.</summary>
    public const string Reporting = "reporting";

    /// <summary>The canonical filter form, interned and reused as the API intends.</summary>
    private static readonly GroupSet ReportingSet = GroupSet.Of(Reporting);

    [Fact]
    public async Task A_spread_collection_expression_materializes_a_single_use_source_once()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        var command = new ReportingOnly();
        var reads = 0;

        await mediator.SendAsync(command, [.. ReadOnce()], CancellationToken.None);

        Assert.True(command.Handled);
        Assert.Equal(1, reads);

        IEnumerable<string> ReadOnce()
        {
            if (++reads != 1)
                throw new InvalidOperationException("The group source cannot be enumerated twice.");
            yield return Reporting;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<MixedAudienceHandler>()
                    .Register<MixedAudienceReportingPre>()
                    .Register<ReportingOnlyHandler>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_default_plan_runs_only_the_ungrouped_participants()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(new MixedAudience(), recorder.Commands());
        Assert.Equal(["handler"], recorder.Stages);
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
                new MixedAudience(), [Reporting], CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_collection_expression_and_a_reused_GroupSet_select_the_same_participants()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var viaSequence = new PipelineRecorder();
        await mediator.SendAsync(new ReportingOnly(), viaSequence.Commands(), [Reporting]);

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
    public async Task The_collection_expression_selects_the_grouped_pipeline_too()
    {
        await using var provider = CreateProvider();
        var command = new ReportingOnly();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command, [Reporting]);

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
