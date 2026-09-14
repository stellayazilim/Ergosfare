namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>The consumer disposed the stream output before enumeration completed.</summary>
/// <remarks>
/// The input producer receives this signal when there is no output consumer left.
/// Stream final interceptors observe it; disposing the output itself does not throw it.
/// </remarks>
public sealed class StreamOutputDisposedException() : ExecutionAbortedException(
    "Stream output was disposed before execution completed.");
