using System.Net;
using System.Text.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Alerting;
using Biogarden.WebApi.Contracts;
using Microsoft.Extensions.Logging;

namespace Biogarden.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IErrorAlertService _errorAlertService;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IErrorAlertService errorAlertService,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _errorAlertService = errorAlertService;
        _logger = logger;
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

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
            ArgumentException => (HttpStatusCode.BadRequest, "Validation failed."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Authentication failed."),
            InvalidOperationException => (HttpStatusCode.Conflict, "Operation conflict."),
            _ => (HttpStatusCode.InternalServerError, "Unexpected server error.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            await NotifyUnexpectedExceptionAsync(context, exception);
        }

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
        await context.Response.WriteAsync(json);
    }

    private async Task NotifyUnexpectedExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        try
        {
            await _errorAlertService.NotifyAsync(new ErrorAlert
            {
                AlertKey = BuildAlertKey(context, exception),
                Source = "Unhandled API exception",
                Summary = $"{exception.GetType().Name} during {context.Request.Method} {context.Request.Path}",
                Level = LogLevel.Error,
                Detail = BuildAlertDetail(context),
                Exception = exception
            });
        }
        catch (Exception alertException)
        {
            _logger.LogError(
                alertException,
                "Failed to send error alert for trace {TraceId}.",
                context.TraceIdentifier);
        }
    }

    private static string BuildAlertKey(HttpContext context, Exception exception)
    {
        var endpoint = context.GetEndpoint()?.DisplayName;
        var location = string.IsNullOrWhiteSpace(endpoint)
            ? $"{context.Request.Method} {context.Request.Path}"
            : endpoint;

        return $"api.unhandled.{exception.GetType().FullName}.{location}";
    }

    private static string BuildAlertDetail(HttpContext context)
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Trace ID: {context.TraceIdentifier}",
            $"Method: {context.Request.Method}",
            $"Path: {context.Request.Path}",
            $"Query string present: {context.Request.QueryString.HasValue}"
        });
    }
}
