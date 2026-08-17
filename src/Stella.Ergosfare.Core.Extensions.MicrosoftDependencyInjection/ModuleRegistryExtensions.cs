namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Extra configuration for an <see cref="IModuleRegistry"/>.
/// </summary>
public static class ModuleRegistryExtensions
{
    /// <summary>
    /// Makes every participant resolve once and be reused for the life of the process,
    /// whatever lifetime it was registered with.
    /// </summary>
    /// <param name="moduleRegistry">The registry being configured.</param>
    /// <returns>The same registry, so calls can be chained.</returns>
    /// <remarks>
    /// This is the fastest dispatch mode and it overrides registered lifetimes: a scoped or
    /// transient participant is constructed once and that instance serves every later
    /// dispatch. Without it, lifetimes are honored — a message whose participants are all
    /// singletons already takes the same fast path, and everything else resolves from the
    /// calling scope.
    /// </remarks>
    public static IModuleRegistry ForceMemoizedHandlers(this IModuleRegistry moduleRegistry)
    {
        if (moduleRegistry is ModuleRegistry concreteRegistry)
        {
            concreteRegistry.MemoizeAllHandlers = true;
        }

        return moduleRegistry;
    }
}
