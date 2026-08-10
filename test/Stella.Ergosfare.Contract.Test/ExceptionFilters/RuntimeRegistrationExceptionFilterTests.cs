using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.ExceptionFilters.Fallback;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.ExceptionFilters;

/// <summary>
/// The same typed exception-interceptor contract under explicit runtime registration — the
/// fallback the generated axis degrades to. These types are excluded from discovery, so no
/// compile-time plan exists for them and the exception stage is the reflective invocation
/// strategy, which asks every resolved instance whether it accepts the thrown exception.
/// </summary>
/// <remarks>
/// The unfiltered interceptor is registered before the filtered one on purpose: if
/// registration order decided the stage, the inherited ordering expectation would fail here
/// and pass on the generated axis.
/// </remarks>
public sealed class RuntimeRegistrationExceptionFilterTests : ExceptionFilterSemanticsContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<FilteredVoidCommandHandler>()
                    .Register<FilteredVoidCommandUnrelated>()
                    .Register<FilteredVoidCommandTagged>()
                    .Register<FilteredVoidCommandFinal>()
                    .Register<FilteredResultCommandHandler>()
                    .Register<FilteredResultCommandUntyped>()
                    .Register<FilteredResultCommandTagged>()
                    .Register<FilteredResultCommandFinal>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IFilteredVoidCommand NewVoidCommand(FaultKind fault)
        => new FilteredVoidCommand { Fault = fault };

    /// <inheritdoc />
    protected override IFilteredResultCommand NewResultCommand(FaultKind fault)
        => new FilteredResultCommand { Fault = fault };
}
