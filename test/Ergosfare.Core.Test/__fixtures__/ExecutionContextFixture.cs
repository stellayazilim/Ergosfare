using Ergosfare.Context;
using Ergosfare.Core.Internal.Contexts;

namespace Ergosfare.Core.Test.__fixtures__;

public class ExecutionContextFixture
{

    public static IExecutionContext CreateExecutionContext()
    {
        AmbientExecutionContext.Current = new ErgosfareExecutionContext(null, CancellationToken.None);
        return AmbientExecutionContext.Current;
    }
}