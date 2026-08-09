using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Context;

/// <summary>
/// What the execution context carries: values written by one stage reaching the later
/// stages of the same dispatch, the caller's cancellation token reaching the handler, and
/// nothing surviving into the next dispatch.
/// </summary>
public sealed class ExecutionContextDataFlowTests
{
    private const string Key = "contract.context";
    private const string WrittenByPre = "written-by-pre";
    private const string WrittenByHandler = "written-by-handler";

    [DiscoveryKey(Key)]
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

    [DiscoveryKey(Key)]
    public sealed class CarrierPre : ICommandPreInterceptor<Carrier>
    {
        public ValueTask<Carrier> HandleAsync(Carrier command, IExecutionContext context)
        {
            context.Set(WrittenByPre, "yes");
            return ValueTask.FromResult(command);
        }
    }

    [DiscoveryKey(Key)]
    public sealed class CarrierHandler : ICommandHandler<Carrier>
    {
        public ValueTask HandleAsync(Carrier command, IExecutionContext context)
        {
            command.HandlerSawPreValue = context.Has(WrittenByPre);
            command.HandlerSawStaleValue = context.Has(WrittenByHandler);
            command.SeenToken = context.CancellationToken;
            context.Set(WrittenByHandler, "yes");
            return ValueTask.CompletedTask;
        }
    }

    [DiscoveryKey(Key)]
    public sealed class CarrierFinal : ICommandFinalInterceptor<Carrier>
    {
        public ValueTask HandleAsync(Carrier command, object? result, Exception? exception, IExecutionContext context)
        {
            command.FinalSawPreValue = context.Has(WrittenByPre);
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
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
            .SendAsync(command, commandMediationSettings: null, cancellation.Token);

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
    public async Task The_callers_settings_items_survive_the_dispatch_and_expose_what_stages_wrote()
    {
        await using var provider = CreateProvider();
        var settings = new CommandMediationSettings();
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

        var first = new CommandMediationSettings();
        first.Items["secret"] = "data";
        await mediator.SendAsync(new Carrier(), first);

        var second = new CommandMediationSettings();
        await mediator.SendAsync(new Carrier(), second);

        Assert.False(second.Items.ContainsKey("secret"));
        Assert.Equal("data", first.Items["secret"]);
    }
}
