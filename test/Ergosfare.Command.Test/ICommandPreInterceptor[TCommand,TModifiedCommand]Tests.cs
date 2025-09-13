using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Context;

namespace Ergosfare.Command.Test;

public class ICommandPreInterceptorTCommandTModifiedCommandTests
{
    class TestPreInterceptor: IAsyncCommandPreInterceptor<StubNonGenericCommand, StubNonGenericCommand>
    {
        public Task<StubNonGenericCommand> HandleAsync(StubNonGenericCommand command, IExecutionContext context)
        {
            
        }
    }
}