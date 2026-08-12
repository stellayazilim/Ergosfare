namespace Stella.Ergosfare.Contract.Test.Runtime;

/// <summary>
/// Serializes the scenarios that reach process-wide state against each other; letting
/// these run beside anything else invites unrelated flakes.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RegistryMutationCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "registry-mutation";
}
