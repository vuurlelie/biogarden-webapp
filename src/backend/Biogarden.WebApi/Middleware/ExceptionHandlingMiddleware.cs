using System.Net;
using System.Text.Json;
using Biogarden.WebApi.Contracts;

namespace Biogarden.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
                }

                return;
            }

            await HandleExceptionAsync(context, exception);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
            ArgumentException => (HttpStatusCode.BadRequest, "Validation failed."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Authentication failed."),
            InvalidOperationException => (HttpStatusCode.Conflict, "Operation conflict."),
            _ => (HttpStatusCode.InternalServerError, "Unexpected server error.")
        };

        var response = new ApiErrorResponse
        {
            Title = title,
            Status = (int)statusCode,
            Detail = exception.Message,
            TraceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response);
        return context.Response.WriteAsync(json);
    }
}
