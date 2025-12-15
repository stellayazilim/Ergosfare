using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Abstractions;

/// <summary>
/// Defines a contract for building handler descriptors from a given type.
/// </summary>
internal interface IHandlerDescriptorBuilder
{
    /// <summary>
    /// Determines whether this builder can create handler descriptors for the specified type.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <returns><c>true</c> if the builder can build descriptors for <paramref name="type"/>; otherwise, <c>false</c>.</returns>
    bool CanBuild(Type type);
    
    /// <summary>
    /// Builds one or more <see cref="IHandlerDescriptor"/> instances from the specified type.
    /// </summary>
    /// <param name="type">The type from which to build handler descriptors.</param>
    /// <returns>A collection of handler descriptors created from <paramref name="type"/>.</returns>
    IEnumerable<IHandlerDescriptor> Build(Type type);
}