using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Declares an assembly to be an Ergosfare plugin and gives it a name.
/// </summary>
/// <remarks>
/// <para>
/// The generator running in the plugin's own compilation reads this and writes the facade
/// consumers call: an <c>IModule</c> implementation and an <c>Add&lt;Name&gt;</c> extension
/// on the module registry. It is a declaration rather than a call because a class library
/// has no entry point to put a call in, and because everything the generator reads has to be
/// in the compilation — as source, or as metadata on a reference.
/// </para>
/// <para>
/// Nothing else needs listing. The services to register are the types carrying
/// <see cref="PipelineInvokableAttribute"/> methods, which the generator already sees, and
/// the hooks and filters are declared on those types and methods.
/// </para>
/// <para>
/// Naming an options type makes the generated <c>Add&lt;Name&gt;</c> take one and registers
/// the instance the consumer passed as a singleton. The type is the plugin author's own —
/// an ordinary class the generator neither writes nor constrains. Both ways of reading it
/// work without wiring: a service can take it as a constructor parameter and receive it once
/// at construction, or a hook method can take it as a parameter and have it resolved at the
/// call site. Because the consumer constructs the instance, what they wrote is what gets
/// registered — no builder in between, and nothing configurable the call site cannot see.
/// </para>
/// </remarks>
/// <example>
/// In the plugin:
/// <code>
/// [assembly: ErgosfarePlugin("Tracing", typeof(TracingOptions))]
/// </code>
/// In the consumer, against the generated facade:
/// <code>
/// services.AddErgosfare(o => o.AddTracing(new TracingOptions { SampleRate = 0.1 }));
/// </code>
/// </example>
/// <param name="name">
/// The plugin's name, used verbatim for the generated <c>Add&lt;Name&gt;</c> method and
/// module type. Must be a valid C# identifier.
/// </param>
/// <param name="optionsType">
/// The plugin's options type, or <c>null</c> when it takes none — in which case
/// <c>Add&lt;Name&gt;</c> is parameterless.
/// </param>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ErgosfarePluginAttribute(string name, Type? optionsType = null) : Attribute
{
    /// <summary>
    /// The plugin's name, as it appears in the generated facade.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// The plugin's options type, or <c>null</c> when it declares none.
    /// </summary>
    public Type? OptionsType => optionsType;
}
