using System;

namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when there is no current execution context available.
/// </summary>
/// <param name="message">Optional message describing the exception. Defaults to "No execution context is set".</param>
public class NoExecutionContextException(string? message = "No execution context is set") : Exception(message);