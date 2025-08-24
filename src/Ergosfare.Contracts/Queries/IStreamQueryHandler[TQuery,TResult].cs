using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface IStreamQueryHandler<in TQuery, out TResult> : IQuery, IStreamHandler<TQuery, TResult> where TQuery : IStreamQuery<TResult>;