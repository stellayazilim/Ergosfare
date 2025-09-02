namespace Ergosfare.Core.Internal.EventHub;

internal interface ISubscription : IDisposable
{
    bool IsAlive { get; }
}