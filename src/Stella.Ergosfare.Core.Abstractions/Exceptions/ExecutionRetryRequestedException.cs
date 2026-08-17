
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// A signal that a pipeline should be run again, carrying the number of attempts made so
/// far.
/// </summary>
/// <remarks>
/// The framework neither raises nor catches this exception; it is a shared shape for
/// retry policies written as interceptors, whose own handling decides what a retry means.
/// </remarks>
/// <param name="counter">How many attempts have been made so far.</param>
public class ExecutionRetryRequestedException( byte counter = 0)
    : Exception($"Pipeline execution requested for a retry, current iteration {counter}.")
{
    /// <summary>
    /// How many attempts had been made when the retry was requested.
    /// </summary>
    public byte Counter => counter;
}
