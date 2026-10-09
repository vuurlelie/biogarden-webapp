using System.Net;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class OperationalMonitoringTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerTheory]
    [InlineData("email-failures")]
    [InlineData("email-stalled")]
    [InlineData("notification")]
    [InlineData("provider-event")]
    [InlineData("ambiguous-payment")]
    [InlineData("refund")]
    public async Task PersistentProblems_AreVisibleWithoutDisablingReadiness_AndClearAfterRecovery(string problem)
    {
        using var client = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/operations")).StatusCode);
        var now = DateTime.UtcNow;
        await using var databaseContext = _app.OpenDatabase();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "monitoring-test",
            Cart = new Cart { Id = Guid.NewGuid(), SessionId = Guid.NewGuid().ToString() }
        };
        databaseContext.Orders.Add(order);
        await databaseContext.SaveChangesAsync();
        switch (problem)
        {
            case "email-failures":
            case "email-stalled":
                databaseContext.OrderEmails.Add(new OrderEmail
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Kind = "customer",
                    Recipient = "private@example.com",
                    Subject = "Private subject",
                    Attempts = problem == "email-failures" ? 5 : 0,
                    NextAttemptAtUtc = problem == "email-stalled" ? now.AddMinutes(-31) : now.AddMinutes(10)
                });
                break;
            case "notification":
                databaseContext.NotificationEmails.Add(new NotificationEmail
                {
                    EventId = Guid.NewGuid(),
                    Kind = "contact",
                    Attempts = 5,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = now.AddHours(1),
                    NextAttemptAtUtc = now
                });
                break;
            case "provider-event":
                databaseContext.PaymentProviderEvents.Add(new PaymentProviderEvent
                {
                    Id = Guid.NewGuid(),
                    Provider = "SumUp",
                    EventKey = "test",
                    ReceivedAtUtc = now.AddMinutes(-16),
                    ProcessingFailedAtUtc = now
                });
                break;
            case "ambiguous-payment":
                databaseContext.PaymentAttempts.Add(new PaymentAttempt
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Reference = "test",
                    CreatedAtUtc = now.AddMinutes(-16),
                    CreateRequested = true
                });
                break;
            case "refund": order.PaymentStatus = PaymentStatus.RefundPending; break;
        }
        await databaseContext.SaveChangesAsync();
        using var response = await client.GetAsync("/health/operations");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        await databaseContext.OrderEmails.ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(orderEmail => orderEmail.SentAtUtc, now));
        await databaseContext.NotificationEmails.ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(notificationEmail => notificationEmail.CompletedAtUtc, now));
        await databaseContext.PaymentProviderEvents.ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(paymentProviderEvent => paymentProviderEvent.ProcessedAtUtc, now));
        await databaseContext.PaymentAttempts.ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(paymentAttempt => paymentAttempt.CheckoutId, "recovered"));
        order.PaymentStatus = PaymentStatus.Refunded;
        await databaseContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/operations")).StatusCode);
    }

    [SqlServerFact]
    public async Task NormalPendingWorkAndCompletedFailures_DoNotRaiseAnIncident()
    {
        var now = DateTime.UtcNow;
        await using var databaseContext = _app.OpenDatabase();
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "normal-monitoring",
            Cart = new Cart { Id = Guid.NewGuid(), SessionId = Guid.NewGuid().ToString() }
        };
        databaseContext.Orders.Add(order);
        await databaseContext.SaveChangesAsync();
        databaseContext.OrderEmails.Add(new OrderEmail
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Kind = "customer",
            Attempts = 10,
            NextAttemptAtUtc = now.AddDays(-1),
            SentAtUtc = now
        });
        databaseContext.PaymentAttempts.Add(new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Reference = "normal-pending",
            CreatedAtUtc = now.AddDays(-1),
            CreateRequested = true,
            CheckoutId = "known-checkout",
            Status = "PENDING"
        });
        databaseContext.NotificationEmails.AddRange(
            new NotificationEmail
            {
                EventId = Guid.NewGuid(),
                Kind = "contact",
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddHours(1),
                Attempts = 1,
                NextAttemptAtUtc = now.AddMinutes(1)
            },
            new NotificationEmail
            {
                EventId = Guid.NewGuid(),
                Kind = "contact",
                CreatedAtUtc = now.AddDays(-1),
                ExpiresAtUtc = now.AddHours(-1),
                Attempts = 8,
                NextAttemptAtUtc = now.AddHours(-1)
            });
        databaseContext.PaymentProviderEvents.Add(new PaymentProviderEvent
        {
            Id = Guid.NewGuid(),
            Provider = "SumUp",
            EventKey = "recent",
            ReceivedAtUtc = now,
            ProcessingFailedAtUtc = now
        });
        await databaseContext.SaveChangesAsync();
        using var client = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/operations")).StatusCode);
    }
}
