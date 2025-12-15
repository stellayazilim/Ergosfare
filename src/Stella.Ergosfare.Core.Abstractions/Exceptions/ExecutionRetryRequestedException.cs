using System;

namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

public class ExecutionRetryRequestedException( byte counter = 0)
    : Exception($"Pipeline execution requested for a retry, current iteration {counter}.")
{
    public byte Counter => counter;
}
