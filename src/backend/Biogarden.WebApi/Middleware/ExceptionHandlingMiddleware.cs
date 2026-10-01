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
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(
        HttpContext context,
        IErrorAlertService errorAlertService)
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

            await HandleExceptionAsync(context, errorAlertService, exception);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        IErrorAlertService errorAlertService,
        Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            GlsDirectoryUnavailableException => (HttpStatusCode.ServiceUnavailable, "Delivery point directory unavailable."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found."),
            ArgumentException => (HttpStatusCode.BadRequest, "Validation failed."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Authentication failed."),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Order data changed."),
            InvalidOperationException => (HttpStatusCode.Conflict, "Operation conflict."),
            _ => (HttpStatusCode.InternalServerError, "Unexpected server error.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            await NotifyUnexpectedExceptionAsync(context, errorAlertService, exception);
        }

        var response = new ApiErrorResponse
        {
            Title = title,
            Status = (int)statusCode,
            Detail = statusCode == HttpStatusCode.InternalServerError
                ? "Váratlan hiba történt. Kérjük, próbáld újra később."
                : exception is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                    ? "Az adatok időközben megváltoztak. Frissítsd az oldalt, és próbáld újra."
                    : exception.Message,
            TraceId = context.TraceIdentifier,
            Instance = context.Request.Path
        };

        LogException(context, exception, statusCode);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await context.Response.WriteAsync(json);
    }

    private void LogException(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode)
    {
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled API exception during {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            return;
        }

        _logger.LogWarning(
            exception,
            "Handled API exception during {Method} {Path}. Response status: {StatusCode}. TraceId: {TraceId}",
            context.Request.Method,
            context.Request.Path,
            (int)statusCode,
            context.TraceIdentifier);
    }

    private async Task NotifyUnexpectedExceptionAsync(
        HttpContext context,
        IErrorAlertService errorAlertService,
        Exception exception)
    {
        try
        {
            await errorAlertService.NotifyAsync(new ErrorAlert
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
