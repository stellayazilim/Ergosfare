using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// What <c>AddErgosfare</c> hands the caller: the place modules are registered and the
/// framework configured.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>
    /// Registers a module.
    /// </summary>
    /// <param name="module">The module to register.</param>
    /// <returns>The same registry, so calls can be chained.</returns>
    IModuleRegistry Register(IModule module);

    /// <summary>
    /// Sets the adapter that result types fall back to when nothing more specific binds
    /// them.
    /// </summary>
    /// <param name="adapterType">
    /// The adapter type: closed and implementing <c>IResultAdapter&lt;TResult&gt;</c>, or an
    /// open generic definition covering a family of result types. It must be concrete and
    /// have a public parameterless constructor; one instance is created per result type
    /// served and kept.
    /// </param>
    /// <returns>The same registry, so calls can be chained.</returns>
    /// <remarks>
    /// The fallback applies only after the message's own <c>[ResultAdapter]</c> annotation
    /// and the built-in <c>Result</c> and <c>Result&lt;T&gt;</c> carriers. A result type the
    /// adapter cannot serve, a message carrying <c>[IgnoreResultAdapter]</c>, and an
    /// application that never calls this all keep the default behavior of throwing failures
    /// rather than returning them.
    /// <para>
    /// This is the call the generator reads, and it reads it at compile time: the argument
    /// must be a literal <c>typeof</c> it can resolve (<c>ERGO019</c>), a compilation names
    /// one fallback adapter (<c>ERGO020</c>), and the adapter must be one generated code can
    /// name and construct (<c>ERGO021</c>). Which result types it serves — and what closes an
    /// open definition over each of them — is answered there and written into the generated
    /// adapter table, which is where the runtime reads it back.
    /// </para>
    /// </remarks>
    [Experimental(ExperimentalIds.ResultAdapterSurface)]
    IModuleRegistry UseDefaultResultAdapter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
        Type adapterType);
}
