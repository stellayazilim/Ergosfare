namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
///     The adapters the framework binds without anyone asking: the native <c>Result</c>
///     carrier's exception adapter. Both the plan layer and the unserved-slot diagnostic
///     consult it, and for the same reason — a slot the framework already serves is not
///     unserved, and is not the container's default adapter's business either.
/// </summary>
internal static class NativeResultAdapters
{
    /// <summary>
    ///     The built-in adapter expression of a native carrier result slot —
    ///     <c>ResultExceptionAdapter</c> for <c>Result</c>, its closed generic twin for
    ///     <c>Result&lt;T&gt;</c> — or <c>false</c> for every other result type.
    /// </summary>
    internal static bool TryGetExpression(string resultTypeExpression, out string? adapterTypeExpression)
    {
        if (resultTypeExpression == EmittedExpressions.NativeResult)
        {
            adapterTypeExpression = EmittedExpressions.NativeResultAdapter;
            return true;
        }

        if (resultTypeExpression.Length > EmittedExpressions.NativeResult.Length + 2
            && resultTypeExpression.StartsWith(EmittedExpressions.NativeResult + "<", StringComparison.Ordinal)
            && resultTypeExpression[resultTypeExpression.Length - 1] == '>')
        {
            adapterTypeExpression = EmittedExpressions.NativeResultAdapter
                + resultTypeExpression.Substring(EmittedExpressions.NativeResult.Length);
            return true;
        }

        adapterTypeExpression = null;
        return false;
    }
}
