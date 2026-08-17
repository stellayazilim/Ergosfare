// ReSharper disable once CheckNamespace

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Lets the compiler accept <c>init</c> accessors, and with them record types, on the
    /// netstandard2.0 target this generator is built for.
    /// </summary>
    internal static class IsExternalInit;

    /// <summary>
    /// Lets the compiler accept <c>required</c> members on the netstandard2.0 target this
    /// generator is built for.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
    internal sealed class RequiredMemberAttribute : Attribute;

    /// <summary>
    /// Records that a construct needs a compiler feature the target framework does not
    /// declare; part of the same <c>required</c>-member support.
    /// </summary>
    /// <param name="featureName">The feature the construct needs.</param>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        /// <summary>
        /// The feature the construct needs.
        /// </summary>
        public string FeatureName { get; } = featureName;
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Marks a constructor as setting every <c>required</c> member, on the netstandard2.0
    /// target this generator is built for.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class SetsRequiredMembersAttribute : Attribute;
}
