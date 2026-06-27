using System.Net;
using UrlShortener.Api.Dtos;

namespace UrlShortener.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions so the API never leaks stack traces, and always
/// returns a consistent JSON error shape. Logs full detail server-side only.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorResponse("internal_error", "An unexpected error occurred."));
        }
    }
}
