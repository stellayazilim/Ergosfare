using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Contracts.Attributes;

namespace Ergosfare.Command.Test.__stubs__;


[Group("group1")]
[Weight(2)]
public class StubCommandPreInterceptor1: ICommandPreInterceptor<StubNonGenericCommand>
{
    public static  bool HasCalled;
    public virtual Task<object> HandleAsync(StubNonGenericCommand message, IExecutionContext context)
    {
        HasCalled = true;
        return Task.FromResult<object>(message);
    }
}


[Group("group1", "group2")]
[Weight(1)]
public class StubCommandPreInterceptor2: ICommandPreInterceptor<StubNonGenericCommand>
{
   
    public static bool HasCalled;
    public virtual Task<object>  HandleAsync(StubNonGenericCommand message, IExecutionContext context)
    {
        HasCalled = true;
        return Task.FromResult<object>(message);
    }
}