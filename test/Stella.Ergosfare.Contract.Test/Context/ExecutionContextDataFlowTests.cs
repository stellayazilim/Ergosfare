using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Context;

// Top-level and unkeyed so the generator bakes each message's plan; every type stays scoped
// to this area and no interceptor targets anything but its own message.

/// <summary>Command whose stages report what the execution context carried to them.</summary>
public sealed class Carrier : ICommand
{
    /// <summary>Whether the handler saw the pre-interceptor's write.</summary>
    public bool HandlerSawPreValue;

    /// <summary>Whether the handler saw a value left behind by an earlier dispatch.</summary>
    public bool HandlerSawStaleValue;

    /// <summary>Whether the final interceptor saw the pre-interceptor's write.</summary>
    public bool FinalSawPreValue;

    /// <summary>The token the handler was given.</summary>
    public CancellationToken SeenToken;
}

/// <inheritdoc />
public sealed class CarrierPre : ICommandPreInterceptor<Carrier>
{
    /// <inheritdoc />
    public ValueTask<Carrier> HandleAsync(Carrier command, ErgosfareContext context)
    {
        context.Set(ExecutionContextDataFlowTests.WrittenByPre, "yes");
        return ValueTask.FromResult(command);
    }
}

/// <inheritdoc />
public sealed class CarrierHandler : ICommandHandler<Carrier>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Carrier command, ErgosfareContext context)
    {
        command.HandlerSawPreValue = context.Has(ExecutionContextDataFlowTests.WrittenByPre);
        command.HandlerSawStaleValue = context.Has(ExecutionContextDataFlowTests.WrittenByHandler);
        command.SeenToken = context.CancellationToken;
        context.Set(ExecutionContextDataFlowTests.WrittenByHandler, "yes");
        return ValueTask.CompletedTask;
    }
}

/// <inheritdoc />
public sealed class CarrierFinal : ICommandFinalInterceptor<Carrier>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Carrier command, object? result, Exception? exception, ErgosfareContext context)
    {
        command.FinalSawPreValue = context.Has(ExecutionContextDataFlowTests.WrittenByPre);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Dispatched from inside another handler, through a child scope.</summary>
public sealed class NestedInner : ICommand;

/// <summary>Aborts its own dispatch and nothing else.</summary>
public sealed class NestedInnerHandler : ICommandHandler<NestedInner>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(NestedInner command, ErgosfareContext context)
    {
        context.Abort();
        return ValueTask.CompletedTask;
    }
}

/// <summary>The outer dispatch, which nests the aborting one.</summary>
public sealed class NestedOuter : ICommand
{
    /// <summary>Whether the outer handler carried on past the nested abort.</summary>
    public bool ReachedTheEnd;

    /// <summary>Whether the outer handler caught the nested abort itself.</summary>
    public bool CaughtTheAbort;

    /// <summary>Whether the outer handler should catch it rather than let it travel.</summary>
    public bool CatchIt;
}

/// <inheritdoc cref="NestedOuter"/>
public sealed class NestedOuterHandler(ICommandMediator mediator) : ICommandHandler<NestedOuter>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(NestedOuter command, ErgosfareContext context)
    {
        using var scope = context.CreateScope();

        if (command.CatchIt)
        {
            try
            {
                await mediator.SendAsync(new NestedInner(), scope.Context);
            }
            catch (ExecutionAbortedException)
            {
                command.CaughtTheAbort = true;
            }
        }
        else
        {
            await mediator.SendAsync(new NestedInner(), scope.Context);
        }

        command.ReachedTheEnd = true;
    }
}

/// <summary>
/// What the execution context carries: values written by one stage reaching the later
/// stages of the same dispatch, the caller's cancellation token reaching the handler, and
/// nothing surviving into the next dispatch.
/// </summary>
public sealed class ExecutionContextDataFlowTests
{
    /// <summary>The context key the pre-interceptor writes.</summary>
    public const string WrittenByPre = "written-by-pre";

    /// <summary>The context key the handler writes.</summary>
    public const string WrittenByHandler = "written-by-handler";

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<CarrierPre>()
                    .Register<CarrierHandler>()
                    .Register<CarrierFinal>()
                    .Register<NestedInnerHandler>()
                    .Register<NestedOuterHandler>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_written_by_a_pre_interceptor_reaches_the_handler_and_the_final_interceptor()
    {
        await using var provider = CreateProvider();
        var command = new Carrier();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.True(command.HandlerSawPreValue);
        Assert.True(command.FinalSawPreValue);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_callers_cancellation_token_is_the_one_the_pipeline_observes()
    {
        await using var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        var command = new Carrier();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(command, cancellation.Token);

        Assert.Equal(cancellation.Token, command.SeenToken);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Context_state_does_not_leak_from_one_dispatch_into_the_next()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var first = new Carrier();
        await mediator.SendAsync(first);

        var second = new Carrier();
        await mediator.SendAsync(second);

        Assert.False(first.HandlerSawStaleValue);
        Assert.False(second.HandlerSawStaleValue);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_callers_items_survive_the_dispatch_and_expose_what_stages_wrote()
    {
        await using var provider = CreateProvider();
        var settings = new ErgosfareContext();
        settings.Items["caller-owned"] = "kept";

        await provider.GetRequiredService<ICommandMediator>().SendAsync(new Carrier(), settings);

        Assert.Equal("kept", settings.Items["caller-owned"]);
        Assert.Equal("yes", settings.Items[WrittenByHandler]);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_second_dispatch_does_not_see_the_first_callers_items()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var first = new ErgosfareContext();
        first.Items["secret"] = "data";
        await mediator.SendAsync(new Carrier(), first);

        var second = new ErgosfareContext();
        await mediator.SendAsync(new Carrier(), second);

        Assert.False(second.Items.ContainsKey("secret"));
        Assert.Equal("data", first.Items["secret"]);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_nested_dispatchs_abort_surfaces_to_the_handler_that_nested_it()
    {
        await using var provider = CreateProvider();
        var command = new NestedOuter();

        // The outer handler is the inner dispatch's call site, so it is who hears that the
        // inner one did not happen. Not catching it ends the outer dispatch too.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await provider.GetRequiredService<ICommandMediator>().SendAsync(command));

        Assert.False(command.ReachedTheEnd);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_handler_that_catches_a_nested_abort_carries_on()
    {
        await using var provider = CreateProvider();
        var command = new NestedOuter { CatchIt = true };

        // Catching it is how a handler says "that inner step was optional" — the outer
        // pipeline then completes normally.
        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.True(command.CaughtTheAbort);
        Assert.True(command.ReachedTheEnd);
    }
}
