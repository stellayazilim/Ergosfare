namespace Ergosfare.Core.Internal.EventHub;

internal interface ISubscription<in TEvent> : ISubscription
{
    bool Invoke(TEvent evt); // returns true if invoked, false if dead

}
