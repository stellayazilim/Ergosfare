using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Core.Test.__stubs__;


internal class StubNonGenericLazyHandler
{
    internal static LazyHandler<StubNonGenericHandler, IHandlerDescriptor> GetLazyInstance()
    {
        return new LazyHandler<StubNonGenericHandler, IHandlerDescriptor>
        {
            Handler = new Lazy<StubNonGenericHandler>(),
            Descriptor = new MainHandlerDescriptor()
            {
                ResultType = typeof(Task),
                MessageType = typeof(StubNonGenericMessage),
                HandlerType = typeof(StubNonGenericHandler)
            }
        };
    }
}

