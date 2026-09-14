namespace Stella.Ergosfare.Events.Abstractions;

/// <summary>
/// Publishes events. An alternative name for <see cref="IEventMediator"/>, adding nothing
/// of its own, for code that reads better asking a publisher to publish.
/// </summary>
public interface IPublisher : IEventMediator;
