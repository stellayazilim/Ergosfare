using System;
using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

internal static class TaskInvocationHelper
{
    public static async Task<object?> AwaitResult(object? result)
    {
        if (result is null) return null;

        if (result is Task<object?> taskObjNullable)
        {
            return await taskObjNullable;
        }

        if (result is Task task)
        {
            await task;
            return null;
        }

        return result;
    }
}
