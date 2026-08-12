using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents a registry that holds and manages modules, allowing registration and configuration.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>
    /// Registers a module with the module registry.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <returns>The instance of the module registry for method chaining.</returns>
    IModuleRegistry Register(IModule module);

    /// <summary>
    /// Configures the application-wide default result adapter: the fallback consulted for
    /// any result slot that binds nothing more specific — no
    /// <c>[ResultAdapter]</c> annotation on the message and not a native
    /// <c>Result</c>/<c>Result&lt;T&gt;</c> carrier. A slot the adapter cannot serve, a
    /// message opting out via <c>[IgnoreResultAdapter]</c>, and an application that never
    /// calls this all keep the classic try/catch semantics — adapters are a recommended
    /// win, never a requirement.
    /// </summary>
    /// <param name="adapterType">
    /// The adapter type: a closed type implementing <c>IResultAdapter&lt;TResult&gt;</c>,
    /// or an open generic definition closed over each served result type (e.g. an adapter
    /// family for a foreign <c>Result&lt;T&gt;</c>). A public parameterless constructor is
    /// required; instances are created per served result type and cached.
    /// </param>
    /// <returns>The instance of the module registry for method chaining.</returns>
    [Experimental(ExperimentalIds.ResultAdapterSurface)]
    IModuleRegistry UseDefaultResultAdapter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
        Type adapterType);
}
