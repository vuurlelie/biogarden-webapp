using System.Net;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class NotificationRecoveryTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task ContactIsDurableAndPartialDeliveryRetryDoesNotRepeatBusinessNotification()
    {
        using var client = _app.Factory.CreateClient();
        var request = Contact();
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/contact", request)).StatusCode);
        Assert.Empty(_app.Sender.Sent);
        await using var db = _app.OpenDatabase();
        Assert.Equal(2, await db.NotificationEmails.CountAsync());
        await _app.RestartApiAsync();
        using var retryClient = _app.Factory.CreateClient();
        _app.Sender.Fail = false;
        // Deliver business first, then force a visitor-only failure.
        await db.NotificationEmails.Where(x => x.Kind == "contact-visitor").ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddHours(1)));
        Assert.True(await Dispatch());
        Assert.Single(_app.Sender.Sent);
        _app.Sender.Fail = true;
        await db.NotificationEmails.Where(x => x.Kind == "contact-visitor").ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await Dispatch());
        Assert.Single(_app.Sender.Sent);
        Assert.Equal(HttpStatusCode.Accepted, (await retryClient.PostAsJsonAsync("/api/contact", request)).StatusCode);
        Assert.Equal(2, await db.NotificationEmails.CountAsync());
        _app.Sender.Fail = false;
        await db.NotificationEmails.Where(x => x.CompletedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await Dispatch());
        Assert.Equal(2, _app.Sender.Sent.Count);
        Assert.All(await db.NotificationEmails.AsNoTracking().ToListAsync(), email =>
        {
            Assert.Equal("sent", email.Outcome);
            Assert.Empty(email.Recipient);
            Assert.Null(email.HtmlBody);
            Assert.Null(email.PlainTextBody);
        });
        request.Message = "Different message reusing the same identifier";
        Assert.Equal(HttpStatusCode.Conflict, (await retryClient.PostAsJsonAsync("/api/contact", request)).StatusCode);
    }

    [SqlServerFact]
    public async Task ConcurrentContactRetriesPersistExactlyOnePair()
    {
        _app.SaveGate.EntityType = typeof(NotificationEmail);
        using var first = _app.Factory.CreateClient();
        using var second = _app.Factory.CreateClient();
        var request = Contact();
        var a = first.PostAsJsonAsync("/api/contact", request);
        var b = second.PostAsJsonAsync("/api/contact", request);
        try { await _app.SaveGate.WaitForBothAsync(); }
        finally { _app.SaveGate.Release(); }
        Assert.All(await Task.WhenAll(a, b), response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        await using var db = _app.OpenDatabase();
        Assert.Equal(2, await db.NotificationEmails.CountAsync());
    }

    [SqlServerFact]
    public async Task ClaimsExcludeOtherWorkersAndExpiredLeasesCanBeRecovered()
    {
        using var client = _app.Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/contact", Contact())).EnsureSuccessStatusCode();
        using var first = _app.Factory.Services.CreateScope();
        using var second = _app.Factory.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<INotificationEmailRepository>();
        var b = second.ServiceProvider.GetRequiredService<INotificationEmailRepository>();
        var now = DateTime.UtcNow;
        var claimed = await a.TryClaimAsync(now, default);
        Assert.NotNull(claimed);
        var other = await b.TryClaimAsync(now, default);
        Assert.NotNull(other);
        Assert.NotEqual(claimed.Id, other.Id);
        Assert.Null(await a.TryClaimAsync(now, default));
        var recovered = await b.TryClaimAsync(now.AddMinutes(3), default);
        Assert.NotNull(recovered);
        var stale = recovered.Id == claimed.Id ? claimed : other;
        await a.CompleteAsync(stale.Id, stale.LeaseId!.Value, now, "sent", default);
        await using var db = _app.OpenDatabase();
        Assert.Null((await db.NotificationEmails.SingleAsync(x => x.Id == recovered.Id)).CompletedAtUtc);
        await b.ExpireAsync(now.AddDays(31), default);
        Assert.All(await db.NotificationEmails.AsNoTracking().ToListAsync(), email =>
        {
            Assert.Equal("expired", email.Outcome);
            Assert.Empty(email.Recipient);
            Assert.Null(email.HtmlBody);
        });
    }

    private async Task<bool> Dispatch()
    {
        using var scope = _app.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationEmailDispatcher>().DispatchNextAsync(default);
    }
    private static ContactMessageRequestDto Contact() => new()
    {
        SubmissionId = Guid.NewGuid(),
        Name = "Visitor",
        Email = "visitor@example.com",
        Message = "Please tell me about your garden products."
    };
}
