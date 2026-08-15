namespace Stella.Ergosfare.Core.Abstractions.DispatchSites;

/// <summary>
/// Assembly-level record of one provable manual registration the source generator observed
/// in the assembly's own source: a <c>Register&lt;T&gt;()</c> or <c>Register(typeof(T))</c>
/// call whose type argument is statically known. Manual registration is the same
/// source-generated collection path as <c>RegisterGenerated()</c> — per type instead of in
/// bulk — so an aggregating composition root counts these types' handler contracts as
/// coverage evidence exactly like discovered ones when judging dead dispatches
/// (ERGO005/006).
/// </summary>
/// <remarks>
/// Written by generated code; not intended to be applied by hand. Registrations whose type
/// cannot be statically known (a non-<c>typeof</c> <c>Type</c> argument, descriptor
/// batches, or the legacy assembly scan of older packages) are instead recorded through
/// <see cref="DispatchManifestAttribute.HasOpaqueRegistrations"/>, which suspends
/// dead-dispatch judgment closure-wide.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ManualRegistrationAttribute(string typeMetadataName) : Attribute
{
    /// <summary>The CLR metadata name of the manually registered type.</summary>
    public string TypeMetadataName { get; } = typeMetadataName;
}
