namespace Stella.Ergosfare.SourceGenerator;

/// <summary>
///     The fully qualified spellings the generated file uses, shared by the layer that
///     reads them off symbols and the layer that bakes them into plans. One declaration
///     each, because a plan comparing a descriptor's result type against its own copy of
///     "ValueTask" is a comparison that silently stops matching the day either moves.
/// </summary>
internal static class EmittedExpressions
{
    internal const string ValueTask = "global::System.Threading.Tasks.ValueTask";

    internal const string Unit = "global::Stella.Ergosfare.Core.Abstractions.Unit";

    internal const string AsyncEnumerator = "global::System.Collections.Generic.IAsyncEnumerator";

    /// <summary>The framework's own <c>Result</c> carrier, and the adapter serving it.</summary>
    internal const string NativeResult = "global::Stella.Ergosfare.Core.Abstractions.Results.Result";

    internal const string NativeResultAdapter =
        "global::Stella.Ergosfare.Core.Abstractions.Results.ResultExceptionAdapter";
}
