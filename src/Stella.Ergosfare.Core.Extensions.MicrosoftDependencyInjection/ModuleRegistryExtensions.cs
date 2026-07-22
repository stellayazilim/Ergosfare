namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides extension methods for configuring and registering modules with an <see cref="IModuleRegistry"/>.
/// </summary>
public static class ModuleRegistryExtensions
{
    /// <summary>
    /// Adds the core messaging module to the specified module registry.
    /// </summary>
    /// <param name="moduleRegistry">
    /// The <see cref="IModuleRegistry"/> instance to which the core messaging module will be registered.
    /// </param>
    /// <param name="builderAction">
    /// An action that configures the core messaging module using a <see cref="CoreModuleBuilder"/>.
    /// </param>
    /// <returns>
    /// The same <see cref="IModuleRegistry"/> instance, enabling fluent chaining.
    /// </returns>
    public static IModuleRegistry AddCoreModule(this IModuleRegistry moduleRegistry,
        Action<IModuleBuilder> builderAction)
    {
        moduleRegistry.Register(new CoreModule(builderAction));

        return moduleRegistry;
    }

    /// <summary>
    /// Enables the deprecated ambient execution context. When enabled, the mediation pipeline
    /// publishes the current <see cref="Stella.Ergosfare.Core.Abstractions.IExecutionContext"/>
    /// through an <c>AsyncLocal</c> on every dispatch, allowing constructor-injected
    /// <c>IExecutionContext</c> services to resolve. This adds a small per-dispatch cost.
    /// </summary>
    /// <param name="moduleRegistry">The module registry being configured.</param>
    /// <returns>The same <see cref="IModuleRegistry"/> instance, enabling fluent chaining.</returns>
    /// <remarks>
    /// Prefer the <c>IExecutionContext</c> parameter passed to handlers and interceptors.
    /// The ambient mechanism is deprecated and will be removed in a future major version.
    /// </remarks>
    public static IModuleRegistry EnableAmbientExecutionContext(this IModuleRegistry moduleRegistry)
    {
#pragma warning disable CS0618 // ambient context is deprecated but supported until removal
        Stella.Ergosfare.Core.Abstractions.AmbientExecutionContext.IsEnabled = true;
#pragma warning restore CS0618

        return moduleRegistry;
    }

    /// <summary>
    /// Restores the pre-v1.2 handler resolution behavior: every handler graph is resolved
    /// once and memoized process-wide, regardless of the handlers' registered DI lifetimes.
    /// This is the fastest dispatch mode, but scoped and transient handler dependencies are
    /// NOT honored — the first resolved instance is reused for all subsequent dispatches.
    /// </summary>
    /// <param name="moduleRegistry">The module registry being configured.</param>
    /// <returns>The same <see cref="IModuleRegistry"/> instance, enabling fluent chaining.</returns>
    /// <remarks>
    /// By default (without this switch) registered lifetimes are honored: messages whose
    /// handlers and interceptors are all singleton-registered use the memoized fast path
    /// automatically, everything else is resolved from the calling scope's provider.
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