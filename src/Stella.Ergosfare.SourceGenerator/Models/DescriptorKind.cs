namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// Which pipeline stage a participant belongs to.
/// </summary>
internal enum DescriptorKind : byte
{
    /// <summary>The participant that handles the message.</summary>
    MainHandler,

    /// <summary>Runs before the main handler.</summary>
    PreInterceptor,

    /// <summary>Runs after the main handler.</summary>
    PostInterceptor,

    /// <summary>Runs when the pipeline fails.</summary>
    ExceptionInterceptor,

    /// <summary>Runs once the pipeline has settled.</summary>
    FinalInterceptor,
}
