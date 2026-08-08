
using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// Process-wide lookup of compile-time-computed handler descriptors, populated by
/// source-generated module initializers: one factory per handler type the generator saw
/// in a compilation. Runtime registration (<c>Register&lt;THandler&gt;()</c> /
/// <c>Register(Type)</c>) consults the catalog first and only falls back to the
/// reflection-based descriptor builders for types no generator modeled — runtime-loaded
/// plugin types, open generics registered manually, foreign assemblies without the
/// analyzer. The generator's descriptor computation mirrors the runtime builders exactly
/// (the contract its test suite pins), so the two paths are interchangeable; the catalog
/// only removes the reflection.
/// </summary>
public static class GeneratedDescriptorCatalog
{
    private static readonly ConcurrentDictionary<Type, Func<IReadOnlyList<IHandlerDescriptor>>> Factories = new();

    /// <summary>
    /// Contributes the descriptor factory of a handler type. Idempotent — the first
    /// contribution for a type wins, which is immaterial because every generator computes
    /// identical descriptors for the same type.
    /// </summary>
    /// <param name="handlerType">The handler type the factory describes.</param>
    /// <param name="descriptorsFactory">Factory producing the type's full descriptor set.</param>
    public static void Add(Type handlerType, Func<IReadOnlyList<IHandlerDescriptor>> descriptorsFactory)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentNullException.ThrowIfNull(descriptorsFactory);

        Factories.TryAdd(handlerType, descriptorsFactory);
    }

    /// <summary>
    /// Materializes the precomputed descriptors of a type, or reports that no generator
    /// modeled it (route to the reflective builders).
    /// </summary>
    internal static bool TryCreateDescriptors(Type type, out IReadOnlyList<IHandlerDescriptor>? descriptors)
    {
        if (Factories.TryGetValue(type, out var factory))
        {
            descriptors = factory();
            return true;
        }

        descriptors = null;
        return false;
    }
}
