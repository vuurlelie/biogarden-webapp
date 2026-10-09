using System.Text.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Alerting;
using Biogarden.WebApi.Contracts;
using Biogarden.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Biogarden.WebApi.Tests;

public sealed class ExceptionHandlingTests
{
    [Theory]
    [InlineData("unexpected", 500)]
    [InlineData("wrapped-database", 500)]
    [InlineData("validation", 400)]
    [InlineData("concurrency", 409)]
    [InlineData("wrapped-concurrency", 409)]
    [InlineData("business-conflict", 409)]
    [InlineData("payment-recovery", 503)]
    [InlineData("non-sql-timeout", 500)]
    public async Task Errors_ExposeOnlySafeDetails_AndRetainTraceId(string failure, int expectedStatus)
    {
        const string internalDetail = "Database internal-server/customer-table is unavailable";
        Exception exception = failure switch
        {
            "wrapped-database" => new InvalidOperationException(internalDetail, new DbUpdateException(internalDetail)),
            "validation" => new ArgumentException("Quantity must be positive."),
            "wrapped-concurrency" => new InvalidOperationException(internalDetail, new DbUpdateConcurrencyException(internalDetail)),
            "concurrency" => new DbUpdateConcurrencyException(internalDetail),
            "business-conflict" => new InvalidOperationException("Stock is reserved."),
            "payment-recovery" => new Biogarden.BusinessLogic.Services.PaymentRecoveryException(),
            "non-sql-timeout" => new TimeoutException(internalDetail),
            _ => new IOException(internalDetail)
        };
        var context = new DefaultHttpContext { TraceIdentifier = "test-trace" };
        using var body = new MemoryStream();
        context.Response.Body = body;
        var alerts = new RecordingAlerts();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.FromException(exception), NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.Invoke(context, alerts);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(response);
        Assert.Equal("test-trace", response.TraceId);
        Assert.Equal(expectedStatus, response.Status);
        Assert.DoesNotContain(internalDetail, response.Detail);
        if (failure is "validation" or "business-conflict" or "payment-recovery")
            Assert.Equal(exception.Message, response.Detail);
        else
            Assert.False(string.IsNullOrWhiteSpace(response.Detail));
        if (expectedStatus == 500)
            Assert.Same(exception, Assert.Single(alerts.Items).Exception);
        else
            Assert.Empty(alerts.Items);
    }

    private sealed class RecordingAlerts : IErrorAlertService
    {
        public List<ErrorAlert> Items { get; } = [];
        public Task NotifyAsync(ErrorAlert alert, CancellationToken cancellationToken = default)
        {
            Items.Add(alert);
            return Task.CompletedTask;
        }
    }
}
