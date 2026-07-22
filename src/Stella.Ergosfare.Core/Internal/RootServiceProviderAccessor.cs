namespace Stella.Ergosfare.Core.Internal;

/// <summary>
/// Captures the root <see cref="IServiceProvider"/>. Registered as a singleton, so the
/// constructor-injected provider is always the root one — used to build process-wide
/// memoized handler graphs without capturing a (disposable) scope provider.
/// </summary>
internal sealed class RootServiceProviderAccessor(IServiceProvider rootProvider)
{
    public IServiceProvider RootProvider { get; } = rootProvider;
}
