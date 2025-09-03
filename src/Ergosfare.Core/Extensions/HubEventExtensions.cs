using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.EventHub;

namespace Ergosfare.Core.Extensions;

public static class HubEventExtensions
{
    public static void Invoke(this HubEvent @event)
    {
        EventHubAccessor.Instance.Publish(@event);
    }
}