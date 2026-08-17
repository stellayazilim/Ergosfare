namespace Stella.Ergosfare.SourceGenerator.ResultAdapters;

/// <summary>
/// The adapters the framework binds on its own, without anyone configuring them.
/// </summary>
/// <remarks>
/// Both the planning layer and the unserved-result diagnostic ask here, for the same
/// reason: a result type the framework already serves is neither unserved nor the
/// container's fallback adapter's concern.
/// </remarks>
internal static class NativeResultAdapters
{
    /// <summary>
    /// Finds the built-in adapter for a result type.
    /// </summary>
    /// <param name="resultTypeExpression">The fully qualified result type.</param>
    /// <param name="adapterTypeExpression">
    /// The adapter to write when this method returns <c>true</c>; otherwise <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> for the framework's own carrier, in either its plain or its payload
    /// form; <c>false</c> for every other result type.
    /// </returns>
    internal static bool TryGetExpression(string resultTypeExpression, out string? adapterTypeExpression)
    {
        if (resultTypeExpression == EmittedExpressions.NativeResult)
        {
            adapterTypeExpression = EmittedExpressions.NativeResultAdapter;
            return true;
        }

        // The payload form: the same carrier with type arguments, whose adapter is its own
        // generic twin closed over the same arguments.
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
