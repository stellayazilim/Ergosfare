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
/// Nothing needs to be listed here. The services to register are the types carrying
/// <see cref="PipelineInvokableAttribute"/> methods, which the generator already sees; the
/// stages, families and keys are declared on those types and methods.
/// </para>
/// </remarks>
/// <example>
/// In the plugin:
/// <code>
/// [assembly: ErgosfarePlugin("Tracing")]
/// </code>
/// In the consumer, against the generated facade:
/// <code>
/// services.AddErgosfare(o => o.AddTracing());
/// </code>
/// </example>
/// <param name="name">
/// The plugin's name, used verbatim for the generated <c>Add&lt;Name&gt;</c> method and the
/// module type. Must be a valid C# identifier.
/// </param>
[Experimental(ExperimentalSurface.Id)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ErgosfarePluginAttribute(string name) : Attribute
{
    /// <summary>The plugin's name, as it appears in the generated facade.</summary>
    public string Name => name;
}
