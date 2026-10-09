using System.Net;
using System.Data.Common;
using Biogarden.DataAccess;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
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
        await using var databaseContext = _app.OpenDatabase();
        Assert.Equal(2, await databaseContext.NotificationEmails.CountAsync());
        await _app.RestartApiAsync();
        using var retryClient = _app.Factory.CreateClient();
        _app.Sender.Fail = false;
        // Deliver business first, then force a visitor-only failure.
        await databaseContext.NotificationEmails.Where(notificationEmail => notificationEmail.Kind == "contact-visitor").ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(notificationEmail => notificationEmail.NextAttemptAtUtc, DateTime.UtcNow.AddHours(1)));
        Assert.True(await Dispatch());
        Assert.Single(_app.Sender.Sent);
        _app.Sender.Fail = true;
        await databaseContext.NotificationEmails.Where(notificationEmail => notificationEmail.Kind == "contact-visitor").ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(notificationEmail => notificationEmail.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await Dispatch());
        Assert.Single(_app.Sender.Sent);
        Assert.Equal(HttpStatusCode.Accepted, (await retryClient.PostAsJsonAsync("/api/contact", request)).StatusCode);
        Assert.Equal(2, await databaseContext.NotificationEmails.CountAsync());
        _app.Sender.Fail = false;
        await databaseContext.NotificationEmails.Where(notificationEmail => notificationEmail.CompletedAtUtc == null).ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(notificationEmail => notificationEmail.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await Dispatch());
        Assert.Equal(2, _app.Sender.Sent.Count);
        Assert.All(await databaseContext.NotificationEmails.AsNoTracking().ToListAsync(), email =>
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
        var firstContactSubmissionTask = first.PostAsJsonAsync("/api/contact", request);
        var secondContactSubmissionTask = second.PostAsJsonAsync("/api/contact", request);
        try { await _app.SaveGate.WaitForBothAsync(); }
        finally { _app.SaveGate.Release(); }
        Assert.All(await Task.WhenAll(firstContactSubmissionTask, secondContactSubmissionTask), response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        await using var databaseContext = _app.OpenDatabase();
        Assert.Equal(2, await databaseContext.NotificationEmails.CountAsync());
    }

    [SqlServerTheory]
    [InlineData(false, HttpStatusCode.Accepted)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task ContactAcceptance_RecoversUniqueConflictAfterConcurrentInsert(bool differentContent, HttpStatusCode expectedStatus)
    {
        var competingInsert = new CompetingContactInsert(_app, differentContent);
        using var api = _app.Factory.WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(competingInsert))));
        using var client = api.CreateClient();
        var request = Contact();

        var response = await client.PostAsJsonAsync("/api/contact", request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(1, competingInsert.UniqueConflicts);
        await using var database = _app.OpenDatabase();
        var persisted = await database.NotificationEmails.AsNoTracking().ToListAsync();
        Assert.Equal(2, persisted.Count);
        Assert.Equal(competingInsert.WinnerIds.OrderBy(id => id), persisted.Select(email => email.Id).OrderBy(id => id));
        Assert.All(persisted, email => Assert.Equal(request.SubmissionId, email.EventId));
        Assert.Equal(2, persisted.Select(email => email.Kind).Distinct().Count());
        Assert.Empty(_app.Sender.Sent);

        // Later replays use the persisted winner without attempting another insert.
        Assert.Equal(expectedStatus, (await client.PostAsJsonAsync("/api/contact", request)).StatusCode);
        Assert.Equal(1, competingInsert.UniqueConflicts);
        Assert.Equal(2, await database.NotificationEmails.CountAsync());
    }

    private sealed class CompetingContactInsert(SqlServerApp app, bool differentContent) : SaveChangesInterceptor
    {
        public List<Guid> WinnerIds { get; } = [];
        public int UniqueConflicts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<NotificationEmail>()
                .Where(entry => entry.State == EntityState.Added).ToList();
            if (pending.Count == 0 || WinnerIds.Count != 0)
                return result;

            // Commit the competing request after the replay read but before this insert.
            // Separate IDs ensure SQL rejects the event/kind key, not the primary key.
            await using var winner = app.OpenDatabase();
            foreach (var entry in pending)
            {
                var email = (NotificationEmail)entry.CurrentValues.ToObject();
                email.Id = Guid.NewGuid();
                if (differentContent)
                    email.RequestHash = new string('0', 64);
                WinnerIds.Add(email.Id);
                winner.NotificationEmails.Add(email);
            }
            await winner.SaveChangesAsync(cancellationToken);
            return result;
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } })
                UniqueConflicts++;
            return Task.CompletedTask;
        }
    }

    [SqlServerTheory]
    [InlineData(1, HttpStatusCode.Accepted, 2)]
    [InlineData(4, HttpStatusCode.ServiceUnavailable, 4)]
    public async Task ContactAcceptance_RetriesSqlDeadlockWithoutDuplicatingQueuedMessages(int failures, HttpStatusCode expectedStatus, int expectedAttempts)
    {
        await using (var seed = _app.OpenDatabase())
        {
            await seed.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.ContactRetryProbe (Id int PRIMARY KEY, Value int NOT NULL); INSERT INTO dbo.ContactRetryProbe VALUES (1, 0), (2, 0);");
        }
        var deadlock = new ContactDeadlock(failures, _app.CreateConnection);
        using var api = _app.Factory.WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(deadlock))));
        using var client = api.CreateClient();
        var request = Contact();
        var response = await client.PostAsJsonAsync("/api/contact", request);
        Assert.True(response.StatusCode == expectedStatus, await response.Content.ReadAsStringAsync());
        if (expectedStatus == HttpStatusCode.ServiceUnavailable)
        {
            var error = (await response.Content.ReadFromJsonAsync<Biogarden.WebApi.Contracts.ApiErrorResponse>())!;
            Assert.Equal("Database temporarily unavailable.", error.Title);
            Assert.Contains("átmenetileg", error.Detail);
            Assert.DoesNotContain("deadlock", error.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
        }
        Assert.Equal(expectedAttempts, deadlock.InsertAttempts);
        await using var database = _app.OpenDatabase();
        Assert.Equal(expectedStatus == HttpStatusCode.Accepted ? 2 : 0, await database.NotificationEmails.CountAsync());
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/contact", request)).StatusCode);
        var attemptsAfterRecovery = expectedStatus == HttpStatusCode.Accepted ? expectedAttempts : expectedAttempts + 1;
        Assert.Equal(attemptsAfterRecovery, deadlock.InsertAttempts);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/contact", request)).StatusCode);
        Assert.Equal(attemptsAfterRecovery, deadlock.InsertAttempts);
        Assert.Equal(2, await database.NotificationEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);
    }

    private sealed class ContactDeadlock(int failures, Func<SqlConnection> createConnection) : DbCommandInterceptor
    {
        public int InsertAttempts;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [NotificationEmails]", StringComparison.Ordinal)
                && Interlocked.Increment(ref InsertAttempts) <= failures)
            {
                // Two test-only rows establish a real lock cycle before the queue insert.
                // Priorities select the victim deterministically; no sleeps or fabricated exceptions.
                await using var victim = createConnection();
                await using var winner = createConnection();
                await victim.OpenAsync(cancellationToken);
                await winner.OpenAsync(cancellationToken);
                await using var victimTransaction = (SqlTransaction)await victim.BeginTransactionAsync(cancellationToken);
                await using var winnerTransaction = (SqlTransaction)await winner.BeginTransactionAsync(cancellationToken);
                await Execute(victim, victimTransaction, "SET DEADLOCK_PRIORITY LOW; UPDATE dbo.ContactRetryProbe SET Value = Value + 1 WHERE Id = 1;", cancellationToken);
                await Execute(winner, winnerTransaction, "SET DEADLOCK_PRIORITY HIGH; UPDATE dbo.ContactRetryProbe SET Value = Value + 1 WHERE Id = 2;", cancellationToken);
                await Task.WhenAll(
                    Execute(victim, victimTransaction, "UPDATE dbo.ContactRetryProbe SET Value = Value + 1 WHERE Id = 2;", cancellationToken),
                    Execute(winner, winnerTransaction, "UPDATE dbo.ContactRetryProbe SET Value = Value + 1 WHERE Id = 1;", cancellationToken)
                );
                throw new InvalidOperationException("The test lock cycle did not produce a deadlock victim.");
            }

            return result;
        }

        private static async Task Execute(SqlConnection connection, SqlTransaction transaction, string sql, CancellationToken token)
        {
            await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 15 };
            await command.ExecuteNonQueryAsync(token);
        }
    }

    [SqlServerFact]
    public async Task ClaimsExcludeOtherWorkersAndExpiredLeasesCanBeRecovered()
    {
        using var client = _app.Factory.CreateClient();
        (await client.PostAsJsonAsync("/api/contact", Contact())).EnsureSuccessStatusCode();
        using var first = _app.Factory.Services.CreateScope();
        using var second = _app.Factory.Services.CreateScope();
        var notificationEmailRepository = first.ServiceProvider.GetRequiredService<INotificationEmailRepository>();
        var secondNotificationEmailRepository = second.ServiceProvider.GetRequiredService<INotificationEmailRepository>();
        var now = DateTime.UtcNow;
        var claimed = await notificationEmailRepository.TryClaimAsync(now, default);
        Assert.NotNull(claimed);
        var other = await secondNotificationEmailRepository.TryClaimAsync(now, default);
        Assert.NotNull(other);
        Assert.NotEqual(claimed.Id, other.Id);
        Assert.Null(await notificationEmailRepository.TryClaimAsync(now, default));
        var recovered = await secondNotificationEmailRepository.TryClaimAsync(now.AddMinutes(3), default);
        Assert.NotNull(recovered);
        var stale = recovered.Id == claimed.Id ? claimed : other;
        await notificationEmailRepository.CompleteAsync(stale.Id, stale.LeaseId!.Value, now, "sent", default);
        await using var databaseContext = _app.OpenDatabase();
        Assert.Null((await databaseContext.NotificationEmails.SingleAsync(notificationEmail => notificationEmail.Id == recovered.Id)).CompletedAtUtc);
        await secondNotificationEmailRepository.ExpireAsync(now.AddDays(31), default);
        Assert.All(await databaseContext.NotificationEmails.AsNoTracking().ToListAsync(), email =>
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
