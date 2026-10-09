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
        await using var databaseContext = _app.OpenDatabase();
        Assert.Empty(await databaseContext.Users.ToListAsync());
        Assert.Empty(await databaseContext.NotificationEmails.ToListAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        var queued = await databaseContext.NotificationEmails.AsNoTracking().SingleAsync();
        Assert.Null(queued.HtmlBody);
        Assert.Null(queued.PlainTextBody); // No confirmation token is persisted.
        Assert.Empty(_app.Sender.Sent);
        Assert.True(await Dispatch());
        Assert.Null((await databaseContext.NotificationEmails.AsNoTracking().SingleAsync()).CompletedAtUtc);
        await _app.RestartApiAsync();
        _app.Sender.Fail = false;
        await MakeDue();
        Assert.True(await Dispatch());
        var message = Assert.Single(_app.Sender.Sent);
        var link = Regex.Match(message.PlainTextBody!, @"https?://[^\s<>]+").Value;
        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        using var retryClient = _app.Factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await retryClient.PostAsJsonAsync("/api/auth/confirm-email", new { userId = queued.UserId, token = query["token"].ToString() })).StatusCode
        );
        Assert.True((await databaseContext.Users.AsNoTracking().SingleAsync()).EmailConfirmed);
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
        await using var databaseContext = _app.OpenDatabase();
        Assert.Equal(user.SecurityStamp, (await databaseContext.Users.AsNoTracking().SingleAsync()).SecurityStamp);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Password123!" })).StatusCode);
        _app.QueueFailure.NotificationsEnabled = false;
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", request)).StatusCode);
        using (var verification = _app.Factory.Services.CreateScope())
            Assert.True((await verification.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(
                new LoginRequestDto { Email = user.Email!, Password = "NewPassword123!" })).Succeeded);
        Assert.Empty(_app.Sender.Sent);
        while (await Dispatch()) { }
        Assert.Equal(
            "suppressed",
            (await databaseContext.NotificationEmails.AsNoTracking().SingleAsync(notificationEmail => notificationEmail.Kind == AccountNotificationQueue.PasswordReset)).Outcome
        );
        _app.Sender.Fail = false;
        await MakeDue();
        Assert.True(await Dispatch());
        Assert.Single(_app.Sender.Sent);
        Assert.Equal(
            "sent",
            (await databaseContext.NotificationEmails.AsNoTracking().SingleAsync(notificationEmail => notificationEmail.Kind == "password-changed")).Outcome
        );
    }

    [SqlServerFact]
    public async Task DeactivationAndDeletionCommitWithTheirNoticesAndDoNotDependOnSmtp()
    {
        var user = await CreateUser();
        using var client = await AuthenticatedClient(user);
        _app.QueueFailure.NotificationsEnabled = true;
        Assert.False((await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })).IsSuccessStatusCode);
        await using var databaseContext = _app.OpenDatabase();
        Assert.Null((await databaseContext.Users.AsNoTracking().SingleAsync()).DeactivatedAtUtc);
        Assert.Empty(await databaseContext.AccountLifecycleRequests.ToListAsync());
        Assert.Empty(await databaseContext.NotificationEmails.ToListAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        _app.Sender.Fail = true;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })).StatusCode);
        Assert.NotNull((await databaseContext.Users.AsNoTracking().SingleAsync()).DeactivatedAtUtc);
        _app.QueueFailure.NotificationsEnabled = true;
        using (var scope = _app.Factory.Services.CreateScope())
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IAdminUserService>().ReactivateAsync(user.Id));
        Assert.NotNull((await databaseContext.Users.AsNoTracking().SingleAsync()).DeactivatedAtUtc);
        Assert.Single(await databaseContext.NotificationEmails.ToListAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        using (var scope = _app.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAdminUserService>().ReactivateAsync(user.Id);
        using var freshClient = await AuthenticatedClient(user);
        _app.QueueFailure.NotificationsEnabled = true;
        Assert.False((await freshClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).IsSuccessStatusCode);
        Assert.Single(await databaseContext.Users.ToListAsync());
        Assert.Equal(2, await databaseContext.NotificationEmails.CountAsync());
        _app.QueueFailure.NotificationsEnabled = false;
        Assert.Equal(HttpStatusCode.OK, (await freshClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).StatusCode);
        Assert.Empty(await databaseContext.Users.ToListAsync());
        Assert.Equal(3, await databaseContext.NotificationEmails.CountAsync());
        Assert.Empty(_app.Sender.Sent);
        _app.Sender.Fail = false;
        while (await Dispatch()) { }
        Assert.Equal(3, _app.Sender.Sent.Count);
        Assert.All(await databaseContext.NotificationEmails.AsNoTracking().ToListAsync(), notificationEmail => Assert.Equal("sent", notificationEmail.Outcome));
    }

    [SqlServerFact]
    public async Task DeferredLinksAreSuppressedAfterAccountChangesAndExpiredRequestsArePurged()
    {
        foreach (var change in new Action<ApplicationUser>[]
        {
            candidateUser => candidateUser.SecurityStamp = Guid.NewGuid().ToString(),
            candidateUser => candidateUser.Email = "changed@example.com",
            candidateUser => candidateUser.DeactivatedAtUtc = DateTime.UtcNow,
            candidateUser => candidateUser.AnonymizedAtUtc = DateTime.UtcNow,
            candidateUser => candidateUser.EmailConfirmed = false,
            candidateUser => { candidateUser.LockoutEnabled = true; candidateUser.LockoutEnd = DateTimeOffset.MaxValue; }
        })
        {
            var user = await CreateUser();
            using (var scope = _app.Factory.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<IAccountNotificationQueue>().QueueAuthenticationAsync(user, AccountNotificationQueue.PasswordReset);
            await using var databaseContext = _app.OpenDatabase();
            var stored = await databaseContext.Users.SingleAsync(candidateUser => candidateUser.Id == user.Id);
            change(stored);
            await databaseContext.SaveChangesAsync();
            Assert.True(await Dispatch());
        }
        var expiredUser = await CreateUser();
        using (var scope = _app.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAccountNotificationQueue>().QueueAuthenticationAsync(expiredUser, AccountNotificationQueue.PasswordReset);
        await using var check = _app.OpenDatabase();
        await check.NotificationEmails.Where(notificationEmail => notificationEmail.CompletedAtUtc == null).ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(notificationEmail => notificationEmail.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        Assert.False(await Dispatch());
        Assert.Empty(_app.Sender.Sent);
        var messages = await check.NotificationEmails.AsNoTracking().ToListAsync();
        Assert.Equal(6, messages.Count(notificationEmail => notificationEmail.Outcome == "suppressed"));
        Assert.Single(messages, candidateNotificationEmail => candidateNotificationEmail.Outcome == "expired");
        Assert.All(messages, candidateNotificationEmail => { Assert.Empty(candidateNotificationEmail.Recipient); Assert.Null(candidateNotificationEmail.SecurityStamp); Assert.Null(candidateNotificationEmail.UserId); });
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
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync(
                        "/api/auth/reset-password",
                        new
                        {
                            userId = user.Id,
                            token = query["token"].ToString(),
                            password = "FreshPassword123!",
                            confirmPassword = "FreshPassword123!"
                        }
                    )).StatusCode
        );
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
        await using var databaseContext = _app.OpenDatabase();
        Assert.Equal(2, await databaseContext.NotificationEmails.CountAsync(notificationEmail => notificationEmail.Outcome == "suppressed"));
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
        await using var databaseContext = _app.OpenDatabase();
        await databaseContext.NotificationEmails.Where(notificationEmail => notificationEmail.CompletedAtUtc == null).ExecuteUpdateAsync(updateSettersBuilder => updateSettersBuilder.SetProperty(notificationEmail => notificationEmail.NextAttemptAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }
}
