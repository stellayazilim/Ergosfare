using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IEventPostInterceptor:  IEvent, IAsyncPostInterceptor<IEvent>;