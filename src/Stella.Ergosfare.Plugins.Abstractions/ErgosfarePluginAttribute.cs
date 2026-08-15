using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Plugins.Abstractions;

/// <summary>
/// Declares the assembly to be an Ergosfare plugin, and names it. The generator running in
/// the plugin's own compilation reads this and emits the module facade the consumer calls:
/// an <c>IModule</c> implementation plus an <c>Add&lt;Name&gt;</c> extension on the module
/// registry.
/// </summary>
/// <remarks>
/// <para>
/// A declaration rather than a call, because a class library has no entry point a
/// <c>CreatePlugin(...)</c> call could sit in, and because everything the generator reads
/// has to live in the input compilation — as source, or as metadata on a reference.
/// </para>
/// <para>
/// Nothing needs to be listed here beyond the name and, when the plugin takes settings, the
/// type carrying them. The services to register are the types carrying
/// <see cref="PipelineInvokableAttribute"/> methods, which the generator already sees; the
/// hooks, families and keys are declared on those types and methods.
/// </para>
/// <para>
/// <b>Settings.</b> Naming an <paramref name="optionsType"/> makes the generated
/// <c>Add&lt;Name&gt;</c> take one and registers the instance the consumer passed as a
/// singleton. The type itself is the plugin author's own — an ordinary class the generator
/// neither writes nor requires anything of. From the registration, both ways of reading it
/// work and neither needs wiring: a service can take it as a constructor parameter and get it
/// once at construction, or a hook method can take it as a parameter and have it resolved
/// from the dispatching provider at the call site.
/// </para>
/// <para>
/// The consumer constructs the instance, so what they wrote is what gets registered — no
/// builder in between, and nothing configurable that the call site cannot see.
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
/// The plugin's name, used verbatim for the generated <c>Add&lt;Name&gt;</c> method and the
/// module type. Must be a valid C# identifier.
/// </param>
/// <param name="optionsType">
/// The plugin's settings type, or <c>null</c> when it takes none — in which case
/// <c>Add&lt;Name&gt;</c> is parameterless, as it is today.
/// </param>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ErgosfarePluginAttribute(string name, Type? optionsType = null) : Attribute
{
    /// <summary>The plugin's name, as it appears in the generated facade.</summary>
    public string Name => name;

    /// <summary>The plugin's settings type, or <c>null</c> when it declares none.</summary>
    public Type? OptionsType => optionsType;
}
