namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
/// The fully qualified names written into generated code.
/// </summary>
/// <remarks>
/// Declared once and shared by the layer that reads them off symbols and the layer that
/// bakes them into plans. Two copies would compare equal until one of them moved, and the
/// mismatch would show up as a plan quietly no longer matching.
/// </remarks>
internal static class EmittedExpressions
{
    /// <summary>
    /// The task type an asynchronous participant returns.
    /// </summary>
    internal const string ValueTask = "global::System.Threading.Tasks.ValueTask";

    /// <summary>
    /// The value a resultless pipeline carries in its result slot.
    /// </summary>
    internal const string Unit = "global::Stella.Ergosfare.Core.Abstractions.Unit";

    /// <summary>
    /// The enumerator a streaming pipeline threads through its stages.
    /// </summary>
    internal const string AsyncEnumerator = "global::System.Collections.Generic.IAsyncEnumerator";

    /// <summary>
    /// The sequence a stream handler produces, and the contract it is called through.
    /// </summary>
    internal const string AsyncEnumerable = "global::System.Collections.Generic.IAsyncEnumerable";

    /// <summary>
    /// The framework's own result carrier.
    /// </summary>
    internal const string NativeResult = "global::Stella.Ergosfare.Core.Abstractions.Results.Result";

    /// <summary>
    /// The adapter that reads the framework's own result carrier.
    /// </summary>
    internal const string NativeResultAdapter =
        "global::Stella.Ergosfare.Core.Abstractions.Results.ResultExceptionAdapter";
}
