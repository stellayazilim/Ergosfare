// ReSharper disable once CheckNamespace

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    ///     Polyfill enabling <c>init</c> accessors (and therefore record types) on the
    ///     netstandard2.0 target this generator is compiled against.
    /// </summary>
    internal static class IsExternalInit;

    /// <summary>
    ///     Polyfill enabling <c>required</c> members on the netstandard2.0 target this
    ///     generator is compiled against.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
    internal sealed class RequiredMemberAttribute : Attribute;

    /// <inheritdoc cref="RequiredMemberAttribute"/>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        public string FeatureName { get; } = featureName;
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    ///     Polyfill for constructors that satisfy <c>required</c> members on the
    ///     netstandard2.0 target this generator is compiled against.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class SetsRequiredMembersAttribute : Attribute;
}
