namespace Stella.Ergosfare.Plugins.Outbox;

/// <summary>Generates reflection-free JSON and registration for a public partial message.</summary>
/// <param name="contract">Stable persisted name; defaults to the fully qualified type name plus /v1.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OutboxMessageAttribute(string? contract = null) : Attribute
{
    public string? Contract { get; } = contract;
}

/// <summary>Compiler-readable codec export for consumers of a separate message assembly.</summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class OutboxCodecManifestAttribute(Type codecType) : Attribute
{
    public Type CodecType { get; } = codecType;
}
