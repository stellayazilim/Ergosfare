using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     A group of types sharing the same effective discovery-key set, emitted under a
///     single key-match guard. Untagged types form the default cluster (the implicit
///     empty-string key), which sorts first.
/// </summary>
internal sealed class Cluster
{
    public Cluster(string signature, List<string> keys)
    {
        Signature = signature;
        Keys = keys;
    }

    public string Signature { get; }

    public List<string> Keys { get; }

    public List<RegistrableTypeModel> Types { get; } = [];
}
