using System;

namespace Ergosfare.Core.Abstractions.Exceptions;




/// <summary>
///     Initializes a new instance of the <see cref="ExecutionAbortedException" /> class.
/// </summary>
public class ExecutionAbortedException(string? message = "Execution was aborted") : Exception(message);