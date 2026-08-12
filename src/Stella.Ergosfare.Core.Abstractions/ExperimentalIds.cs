namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The diagnostic ids behind the framework's <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>
/// markings — one id per experimental surface, so consumers opt in with a single,
/// documented suppression instead of chasing members.
/// </summary>
public static class ExperimentalIds
{
    /// <summary>
    /// The declarative result-adapter surface: <c>[ResultAdapter]</c>,
    /// <c>[IgnoreResultAdapter]</c> and the <c>UseDefaultResultAdapter</c> configuration
    /// with its <c>DefaultResultAdapter</c> carrier. The value channel's semantics are
    /// settled, but this binding surface is young — shapes may still shift in a minor
    /// release. Opt in per project with <c>&lt;NoWarn&gt;$(NoWarn);ERGOEXP001&lt;/NoWarn&gt;</c>
    /// or per site with <c>#pragma warning disable ERGOEXP001</c>.
    /// </summary>
    public const string ResultAdapterSurface = "ERGOEXP001";
}
