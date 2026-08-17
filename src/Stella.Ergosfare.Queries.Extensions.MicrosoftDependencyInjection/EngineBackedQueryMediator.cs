using Stella.Ergosfare.Core;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// <see cref="QueryMediator"/> with exactly one constructor, which is what the container
/// registers.
/// </summary>
/// <param name="engine">The container's dispatch engine.</param>
/// <param name="serviceProvider">The provider of the scope being served.</param>
/// <remarks>
/// Having a single constructor lets container activation bind without choosing between
/// overloads, and without the per-resolution lookup a factory registration would cost.
/// </remarks>
internal sealed class EngineBackedQueryMediator(
    MessageDispatchEngine engine,
    IServiceProvider serviceProvider)
    : QueryMediator(engine, serviceProvider);
