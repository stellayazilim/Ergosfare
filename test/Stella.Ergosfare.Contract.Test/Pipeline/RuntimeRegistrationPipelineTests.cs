using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Pipeline.Fallback;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Pipeline;

/// <summary>
/// The same pipeline contract under explicit runtime registration — the fallback the
/// generated axis degrades to. These types are excluded from discovery, so no generated
/// descriptor, dispatch root or compile-time plan exists for them and every stage is
/// resolved reflectively.
/// </summary>
/// <remarks>
/// The tie-break scenario registers <c>OrderedTieOmegaPre</c> before
/// <c>OrderedTieAlphaPre</c> on purpose: if registration order decided equal weights, the
/// inherited expectation would fail here and pass on the generated axis.
/// </remarks>
public sealed class RuntimeRegistrationPipelineTests : PipelineSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<PipelineCommandHandler>()
                    .Register<PipelineCommandPre>()
                    .Register<PipelineCommandPost>()
                    .Register<PipelineCommandException>()
                    .Register<PipelineCommandFinal>()
                    .Register<BarePipelineCommandHandler>()
                    .Register<PipelineResultCommandHandler>()
                    .Register<PipelineResultCommandPre>()
                    .Register<PipelineResultCommandPost>()
                    .Register<PipelineResultCommandException>()
                    .Register<PipelineResultCommandFinal>()
                    .Register<BarePipelineResultCommandHandler>()
                    .Register<OrderedCommandHandler>()
                    .Register<OrderedTieOmegaPre>()
                    .Register<OrderedTieAlphaPre>()
                    .Register<OrderedLightPre>()
                    .Register<OrderedHeavyPre>()
                    .Register<OrderedLowPost>()
                    .Register<OrderedHighPost>())
                .AddQueryModule(queries => queries
                    .Register<PipelineValueQueryHandler>()
                    .Register<PipelineValueQueryPre>()
                    .Register<PipelineValueQueryPost>()
                    .Register<PipelineValueQueryException>()
                    .Register<PipelineValueQueryFinal>()
                    .Register<BarePipelineValueQueryHandler>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IPayloadCommand NewCommand(string payload) => new PipelineCommand { Payload = payload };

    /// <inheritdoc />
    protected override IPayloadCommand NewBareCommand(string payload) => new BarePipelineCommand { Payload = payload };

    /// <inheritdoc />
    protected override IPayloadResultCommand NewResultCommand(string payload)
        => new PipelineResultCommand { Payload = payload };

    /// <inheritdoc />
    protected override IPayloadResultCommand NewBareResultCommand(string payload)
        => new BarePipelineResultCommand { Payload = payload };

    /// <inheritdoc />
    protected override IPayloadValueQuery NewValueQuery(string payload)
        => new PipelineValueQuery { Payload = payload };

    /// <inheritdoc />
    protected override IPayloadValueQuery NewBareValueQuery(string payload)
        => new BarePipelineValueQuery { Payload = payload };

    /// <inheritdoc />
    protected override ICommand NewOrderedCommand() => new OrderedCommand();
}
