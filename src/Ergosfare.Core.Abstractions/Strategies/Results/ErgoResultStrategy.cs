using System;
using System.Collections.Generic;
using System.Linq;
using Ergosfare.Contracts;

namespace Ergosfare.Core.Abstractions.Strategies;

public class ErgoResultStrategy<T> : IResultStrategy
{
    public IReadOnlyList<Exception> Exceptions { get; private set; } = [];
    public bool IsSuccess(object? result)
    {
        if (result is ErgoResult ergoResult)
        {
            if (ergoResult.IsSuccess)
                return true;

            Exceptions = ergoResult.Errors?.ToList() ?? [];
        }

        return false;
    }
}