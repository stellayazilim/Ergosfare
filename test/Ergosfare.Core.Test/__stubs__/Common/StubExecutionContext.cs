using Ergosfare.Context;
using Ergosfare.Core.Internal.Contexts;

namespace Ergosfare.Core.Test.__stubs__;

public class StubExecutionContext
{
    public static IExecutionContext Create() => new ErgosfareExecutionContext(
        new Dictionary<object, object?>(), CancellationToken.None);
}