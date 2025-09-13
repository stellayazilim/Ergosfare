using System.Runtime.InteropServices.ComTypes;
using Ergosfare.Contracts;
using Ergosfare.Queries.Abstractions;

namespace Ergosfare.Queries.Test.__stubs__;

public record StubNonGenericStringResultQuery: IQuery<string>;
public record StubNonGenericStreamStringResultQuery: IStreamQuery<string>;