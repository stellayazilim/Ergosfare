using Ergosfare.Core.Abstractions.SignalHub;

namespace Ergosfare.Core.Extensions;

public static class SignalExtensions
{
    public static void Invoke(this Signal @event)
    {
        SignalHubAccessor.Instance.Publish(@event);
    }
}