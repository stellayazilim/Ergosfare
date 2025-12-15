using System;

namespace Stella.Ergosfare.Core.Abstractions;


/// <summary>
/// Defines a contract for adapting and inspecting result objects of arbitrary types
/// to extract exceptions without throwing them directly.
/// </summary>
/// <remarks>
/// The primary purpose of a result adapter is to enable the framework to invoke
/// exception interceptors based on exceptions contained within result objects, 
/// without having to throw these exceptions. This allows the message handling 
/// pipeline to process results and exceptions consistently, regardless of whether
/// the exception occurred naturally or is wrapped inside a result type.
///
/// Implementations of this interface allow the pipeline to remain agnostic to the
/// specific result type returned by handlers, supporting result wrapper types 
/// such as <c>FluentResult</c>, <c>OneOf</c>, or any custom domain-specific result object.
/// 
/// Multiple adapters can be registered in an <see cref="IResultAdapterService"/>, and
/// each adapter is evaluated in order until one indicates it can adapt the result
/// and successfully extracts an exception.
/// </remarks>
public interface IResultAdapter
   
{
   /// <summary>
   /// Determines whether this adapter can handle the provided <paramref name="result"/> object.
   /// </summary>
   /// <param name="result">The result object to evaluate. This may be any object type.</param>
   /// <returns>
   /// <c>true</c> if this adapter can process the given result; otherwise, <c>false</c>.
   /// </returns>
   bool CanAdapt(object result); 
   
   
   /// <summary>
   /// Attempts to extract an <see cref="Exception"/> from the given result object
   /// without throwing it.
   /// </summary>
   /// <param name="result">The result object to inspect.</param>
   /// <param name="exception">The exception extracted from the result, if found.</param>
   /// <returns>
   /// <c>true</c> if an exception was successfully extracted; otherwise, <c>false</c>.
   /// </returns>
   /// <remarks>
   /// This method should only be called after <see cref="CanAdapt"/> returns <c>true</c>
   /// for the same result object. Implementations should handle the logic for retrieving
   /// exceptions from wrapped or custom result types, allowing the framework to invoke
   /// exception interceptors without throwing.
   /// </remarks>
   bool TryGetException(object result, out Exception? exception);
}