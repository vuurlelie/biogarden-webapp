using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class AccountNotificationRecoveryTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task RegistrationRollsBackOnQueueFailureAndSurvivesSmtpFailureAndRestart()
    {
        using var client = _app.Factory.CreateClient();
        var request = new { email = "registration@example.com", password = "Password123!", confirmPassword = "Password123!" };
        _app.QueueFailure.NotificationsEnabled = true;
        Assert.False((await client.PostAsJsonAsync("/api/auth/register", request)).IsSuccessStatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.NotificationEmails.ToListAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        var queued = await db.NotificationEmails.AsNoTracking().SingleAsync();
        Assert.Null(queued.HtmlBody);
        Assert.Null(queued.PlainTextBody); // No confirmation token is persisted.
        Assert.Empty(_app.Sender.Sent);
        Assert.True(await Dispatch());
        Assert.Null((await db.NotificationEmails.AsNoTracking().SingleAsync()).CompletedAtUtc);
        await _app.RestartApiAsync();
        _app.Sender.Fail = false;
        await MakeDue();
        Assert.True(await Dispatch());
        var message = Assert.Single(_app.Sender.Sent);
        var link = Regex.Match(message.PlainTextBody!, @"https?://[^\s<>]+").Value;
        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        using var retryClient = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await retryClient.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            userId = queued.UserId,
            token = query["token"].ToString()
        })).StatusCode);
        Assert.True((await db.Users.AsNoTracking().SingleAsync()).EmailConfirmed);
    }

    [SqlServerFact]
    public async Task ResetRollsBackOnQueueFailureAndInvalidatesQueuedResetLinksOnSuccess()
    {
        var user = await CreateUser();
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var token = await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(user.Id.ToString()))!);
        using var client = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "missing@example.com" })).StatusCode);
        var request = new { userId = user.Id, token, password = "NewPassword123!", confirmPassword = "NewPassword123!" };
        _app.QueueFailure.NotificationsEnabled = true;
        Assert.False((await client.PostAsJsonAsync("/api/auth/reset-password", request)).IsSuccessStatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Equal(user.SecurityStamp, (await db.Users.AsNoTracking().SingleAsync()).SecurityStamp);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Password123!" })).StatusCode);
        _app.QueueFailure.NotificationsEnabled = false;
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", request)).StatusCode);
        using (var verification = _app.Factory.Services.CreateScope())
            Assert.True((await verification.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(
                new LoginRequestDto { Email = user.Email!, Password = "NewPassword123!" })).Succeeded);
        Assert.Empty(_app.Sender.Sent);
        while (await Dispatch()) { }
        Assert.Equal("suppressed", (await db.NotificationEmails.AsNoTracking().SingleAsync(x => x.Kind == AccountNotificationQueue.PasswordReset)).Outcome);
        _app.Sender.Fail = false;
        await MakeDue();
        Assert.True(await Dispatch());
        Assert.Single(_app.Sender.Sent);
        Assert.Equal("sent", (await db.NotificationEmails.AsNoTracking().SingleAsync(x => x.Kind == "password-changed")).Outcome);
    }

    [SqlServerFact]
    public async Task DeactivationAndDeletionCommitWithTheirNoticesAndDoNotDependOnSmtp()
    {
        var user = await CreateUser();
        using var client = await AuthenticatedClient(user);
        _app.QueueFailure.NotificationsEnabled = true;
        Assert.False((await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })).IsSuccessStatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Null((await db.Users.AsNoTracking().SingleAsync()).DeactivatedAtUtc);
        Assert.Empty(await db.AccountLifecycleRequests.ToListAsync());
        Assert.Empty(await db.NotificationEmails.ToListAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })).StatusCode);
        Assert.NotNull((await db.Users.AsNoTracking().SingleAsync()).DeactivatedAtUtc);
        _app.QueueFailure.NotificationsEnabled = true;
        using (var scope = _app.Factory.Services.CreateScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IAdminUserService>().ReactivateAsync(user.Id));
        Assert.NotNull((await db.Users.AsNoTracking().SingleAsync()).DeactivatedAtUtc);
        Assert.Single(await db.NotificationEmails.ToListAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        using (var scope = _app.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAdminUserService>().ReactivateAsync(user.Id);
        using var freshClient = await AuthenticatedClient(user);
        _app.QueueFailure.NotificationsEnabled = true;
        Assert.False((await freshClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).IsSuccessStatusCode);
        Assert.Single(await db.Users.ToListAsync());
        Assert.Equal(2, await db.NotificationEmails.CountAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        Assert.Equal(HttpStatusCode.OK, (await freshClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).StatusCode);
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Equal(3, await db.NotificationEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);
        _app.Sender.Fail = false;
        while (await Dispatch()) { }
        Assert.Equal(3, _app.Sender.Sent.Count);
        Assert.All(await db.NotificationEmails.AsNoTracking().ToListAsync(), x => Assert.Equal("sent", x.Outcome));
    }

    [SqlServerFact]
    public async Task DeferredLinksAreSuppressedAfterAccountChangesAndExpiredRequestsArePurged()
    {
        foreach (var change in new Action<ApplicationUser>[]
        {
            u => u.SecurityStamp = Guid.NewGuid().ToString(),
            u => u.Email = "changed@example.com",
            u => u.DeactivatedAtUtc = DateTime.UtcNow,
            u => u.AnonymizedAtUtc = DateTime.UtcNow,
            u => u.EmailConfirmed = false,
            u => { u.LockoutEnabled = true; u.LockoutEnd = DateTimeOffset.MaxValue; }
        })
        {
            var user = await CreateUser();
            using (var scope = _app.Factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IAccountNotificationQueue>().QueueAuthenticationAsync(user, AccountNotificationQueue.PasswordReset);
            await using var db = _app.OpenDatabase();
            var stored = await db.Users.SingleAsync(x => x.Id == user.Id);
            change(stored);
            await db.SaveChangesAsync();
            Assert.True(await Dispatch());
        }
        var expiredUser = await CreateUser();
        using (var scope = _app.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAccountNotificationQueue>().QueueAuthenticationAsync(expiredUser, AccountNotificationQueue.PasswordReset);
        await using var check = _app.OpenDatabase();
        await check.NotificationEmails.Where(x => x.CompletedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.False(await Dispatch());
        Assert.Empty(_app.Sender.Sent);
        var messages = await check.NotificationEmails.AsNoTracking().ToListAsync();
        Assert.Equal(6, messages.Count(x => x.Outcome == "suppressed"));
        Assert.Single(messages, x => x.Outcome == "expired");
        Assert.All(messages, x => { Assert.Empty(x.Recipient); Assert.Null(x.SecurityStamp); Assert.Null(x.UserId); });
    }

    [SqlServerFact]
    public async Task FreshResetLinkWorksButDeletedUsersAndConfirmedAccountsDoNotReceiveObsoleteLinks()
    {
        var user = await CreateUser();
        using var client = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email })).StatusCode);
        Assert.True(await Dispatch());
        var message = Assert.Single(_app.Sender.Sent);
        var link = Regex.Match(message.PlainTextBody!, @"https?://[^\s<>]+").Value;
        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = user.Id,
            token = query["token"].ToString(),
            password = "FreshPassword123!",
            confirmPassword = "FreshPassword123!"
        })).StatusCode);
        Assert.True(await Dispatch()); // Password-changed notice.
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IAccountNotificationQueue>();
            var current = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString()))!;
            await queue.QueueAuthenticationAsync(current, AccountNotificationQueue.Confirmation);
        }
        Assert.True(await Dispatch()); // Already confirmed.
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var current = (await users.FindByIdAsync(user.Id.ToString()))!;
            await scope.ServiceProvider.GetRequiredService<IAccountNotificationQueue>().QueueAuthenticationAsync(current, AccountNotificationQueue.PasswordReset);
            Assert.True((await users.DeleteAsync(current)).Succeeded);
        }
        Assert.True(await Dispatch());
        Assert.Equal(2, _app.Sender.Sent.Count);
        await using var db = _app.OpenDatabase();
        Assert.Equal(2, await db.NotificationEmails.CountAsync(x => x.Outcome == "suppressed"));
    }

    private async Task<ApplicationUser> CreateUser()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var email = $"notification-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Password123!")).Succeeded);
        return user;
    }
    private async Task<HttpClient> AuthenticatedClient(ApplicationUser user)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var auth = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new LoginRequestDto { Email = user.Email!, Password = "Password123!" });
        Assert.True(auth.Succeeded);
        var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AuthResponse!.Token);
        return client;
    }
    private async Task<bool> Dispatch()
    {
        using var scope = _app.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationEmailDispatcher>().DispatchNextAsync(default);
    }
    private async Task MakeDue()
    {
        await using var db = _app.OpenDatabase();
        await db.NotificationEmails.Where(x => x.CompletedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }
}
