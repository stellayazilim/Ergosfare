using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Stella.Ergosfare.E2E.Domain;

namespace Stella.Ergosfare.E2E.Api;

/// <summary>
/// Translates the domain exceptions that surface out of the mediator pipeline into HTTP
/// problem responses. Anything it does not recognise is left to the default handler.
/// </summary>
public sealed class TodoExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            TodoValidationException => (StatusCodes.Status400BadRequest, exception.Message),
            TodoNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            _ => (0, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title }, cancellationToken);

        return true;
    }
}
