using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.ExceptionFilters.Generated;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.ExceptionFilters;

/// <summary>
/// The typed exception-interceptor contract under source-generated registration — the
/// primary axis. The generator reads each interceptor's declared exception type at compile
/// time and bakes it into the emitted plan as an <c>is</c> guard, so this axis never asks
/// an instance anything.
/// </summary>
/// <remarks>
/// The pattern-less overload is deliberate and is reserved for this axis: plan eligibility
/// requires default-discovery types, so a keyed selection would quietly fall back to the
/// reflective executors and stop testing the emitted guard.
/// </remarks>
public sealed class GeneratedRegistrationExceptionFilterTests : ExceptionFilterSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IFilteredVoidCommand NewVoidCommand(FaultKind fault)
        => new FilteredVoidCommand { Fault = fault };

    /// <inheritdoc />
    protected override IFilteredResultCommand NewResultCommand(FaultKind fault)
        => new FilteredResultCommand { Fault = fault };
}
