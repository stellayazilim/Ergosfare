using Stella.Ergosfare.SourceGenerator.Models;

namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The types that share one discovery-key set, written out together under a single key
/// test.
/// </summary>
/// <remarks>
/// Types declaring no key form the default cluster — the implicit empty-string key — which
/// is written first.
/// </remarks>
internal sealed class Cluster
{
    /// <summary>
    /// Initializes a cluster for one key set.
    /// </summary>
    /// <param name="signature">The key set as a single comparable string.</param>
    /// <param name="keys">The keys themselves, for writing the test.</param>
    public Cluster(string signature, List<string> keys)
    {
        Signature = signature;
        Keys = keys;
    }

    /// <summary>
    /// The key set as a single string, which is what groups types into this cluster.
    /// </summary>
    public string Signature { get; }

    /// <summary>
    /// The keys this cluster's test matches against.
    /// </summary>
    public List<string> Keys { get; }

    /// <summary>
    /// The types registered under this cluster's test.
    /// </summary>
    public List<RegistrableTypeModel> Types { get; } = [];
}
