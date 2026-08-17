namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Records one hand-written registration the source generator could resolve at compile
/// time: a <c>Register&lt;T&gt;()</c> or <c>Register(typeof(T))</c> call whose type is
/// statically known.
/// </summary>
/// <remarks>
/// Registering a type by hand collects it through the same generated path as
/// <c>RegisterGenerated()</c>, one type at a time instead of in bulk, so a composition root
/// counts these types as evidence of coverage exactly like discovered ones when reporting
/// dead dispatches (ERGO005 and ERGO006).
/// <para>
/// Written by generated code; do not apply it by hand. Registrations whose type cannot be
/// known at compile time are recorded through
/// <see cref="DispatchManifestAttribute.HasOpaqueRegistrations"/> instead, which stops
/// dead-dispatch reporting across the whole program.
/// </para>
/// </remarks>
/// <param name="typeMetadataName">The CLR metadata name of the registered type.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ManualRegistrationAttribute(string typeMetadataName) : Attribute
{
    /// <summary>
    /// The CLR metadata name of the registered type.
    /// </summary>
    public string TypeMetadataName { get; } = typeMetadataName;
}
