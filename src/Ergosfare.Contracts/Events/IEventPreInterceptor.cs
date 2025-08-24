using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventPreInterceptor: IEvent, IAsyncPreInterceptor<IEvent>;