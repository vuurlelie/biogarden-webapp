using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class SessionRevocationTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task ActiveSession_CanReadOwnProfile_ButCannotAdministerOrders()
    {
        var user = await CreateUserAsync();
        using var client = await ClientAsync(user);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders")).StatusCode);
        using var anonymous = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/profile")).StatusCode);
    }

    [SqlServerFact]
    public async Task Deactivation_RevokesOldSession_EvenAfterReactivation()
    {
        var user = await CreateUserAsync();
        using var oldClient = await ClientAsync(user);
        var response = await oldClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/profile")).StatusCode);
        using (var scope = _app.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAdminUserService>().ReactivateAsync(user);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/profile")).StatusCode);
        using var freshClient = await ClientAsync(user);
        Assert.Equal(HttpStatusCode.OK, (await freshClient.GetAsync("/api/profile")).StatusCode);
    }

    [SqlServerFact]
    public async Task Erasure_RevokesSessionForDeletedUser()
    {
        var user = await CreateUserAsync();
        using var client = await ClientAsync(user);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [SqlServerFact]
    public async Task PasswordReset_RevokesOldSession_AndNewLoginWorks()
    {
        var userId = await CreateUserAsync();
        using var client = await ClientAsync(userId);
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(userId.ToString()))!;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var result = await auth.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            UserId = userId,
            Token = token,
            Password = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });
        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        var login = await auth.LoginAsync(new LoginRequestDto { Email = user.Email!, Password = "NewPassword123!" });
        Assert.True(login.Succeeded);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AuthResponse!.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/profile")).StatusCode);
    }

    [SqlServerFact]
    public async Task CurrentAccountState_IsCheckedEvenWhenStampHasNotChanged()
    {
        foreach (var change in new Action<ApplicationUser>[]
        {
            user => user.AnonymizedAtUtc = DateTime.UtcNow,
            user => user.DeactivatedAtUtc = DateTime.UtcNow,
            user => user.EmailConfirmed = false,
            user => { user.LockoutEnabled = true; user.LockoutEnd = DateTimeOffset.MaxValue; }
        })
        {
            var id = await CreateUserAsync();
            using var client = await ClientAsync(id);
            using var scope = _app.Factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!;
            change(user);
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile")).StatusCode);
        }
    }

    private async Task<Guid> CreateUserAsync(bool admin = false)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"session-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "Password123!")).Succeeded);
        if (admin)
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Admin"))
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        return user.Id;
    }

    private async Task<HttpClient> ClientAsync(Guid userId)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString()))!;
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
