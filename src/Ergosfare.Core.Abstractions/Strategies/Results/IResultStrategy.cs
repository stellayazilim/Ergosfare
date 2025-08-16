using System;
using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.Strategies;

public interface IResultStrategy
{
    IReadOnlyList<Exception> Exceptions { get; }
    bool IsSuccess(object? result);
}