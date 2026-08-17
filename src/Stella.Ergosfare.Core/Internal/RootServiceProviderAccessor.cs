namespace Stella.Ergosfare.Core.Internal;

/// <summary>
/// Holds the container's root <see cref="IServiceProvider"/>.
/// </summary>
/// <param name="rootProvider">The provider injected into this accessor.</param>
/// <remarks>
/// Registered as a singleton, so the provider injected here is the root one. Memoized
/// participants resolve from it, which keeps them off a scope provider that will be
/// disposed.
/// </remarks>
internal sealed class RootServiceProviderAccessor(IServiceProvider rootProvider)
{
    /// <summary>
    /// The container's root provider.
    /// </summary>
    public IServiceProvider RootProvider { get; } = rootProvider;
}
