using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Pipeline;

/// <summary>
/// The pipeline contract under source-generated registration — the primary axis. The
/// generator discovers these types at compile time and <c>RegisterGenerated()</c> installs
/// pre-computed descriptors, dispatch roots and the compile-time pipeline plans.
/// </summary>
/// <remarks>
/// The pattern-less overload is deliberate and is reserved for this axis: plan eligibility
/// requires default-discovery types, so a keyed selection would quietly fall back to the
/// reflective executors. It is safe because every other construct in the assembly is keyed
/// or excluded from discovery.
/// </remarks>
public sealed class GeneratedRegistrationPipelineTests : PipelineSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated())
                .AddQueryModule(queries => queries.RegisterGenerated()))
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

    /// <inheritdoc />
    protected override IPayloadCommand NewAsyncTypedCommand(string payload)
        => new AsyncTypedPipelineCommand { Payload = payload };
}
