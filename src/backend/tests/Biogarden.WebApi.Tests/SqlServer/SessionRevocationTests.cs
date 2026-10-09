using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Biogarden.WebApi.Filters;

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
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).StatusCode);
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
            var login = await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .LoginAsync(new LoginRequestDto { Email = user.Email!, Password = "Password123!" });
            Assert.False(login.Succeeded);
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

    [SqlServerFact]
    public async Task RoleChanges_RevokeOldTokens_AndFreshTokensUseCurrentRoles()
    {
        var actor = await CreateUserAsync(admin: true);
        var target = await CreateUserAsync(admin: true);
        using var actorClient = await ClientAsync(actor);
        using var oldAdmin = await ClientAsync(target);
        Assert.Equal(HttpStatusCode.OK, (await actorClient.PostAsync($"/api/admin/users/{target}/admin-role?isAdmin=false", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldAdmin.GetAsync("/api/admin/orders")).StatusCode);
        using var customer = await ClientAsync(target);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await actorClient.PostAsync($"/api/admin/users/{target}/admin-role?isAdmin=true", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/profile")).StatusCode);
        using var newAdmin = await ClientAsync(target);
        Assert.Equal(HttpStatusCode.OK, (await newAdmin.GetAsync("/api/admin/orders")).StatusCode);
    }

    [SqlServerFact]
    public async Task RoleChangesOutsideAdminService_AreAlsoDetected()
    {
        var id = await CreateUserAsync(admin: true);
        using var client = await ClientAsync(id);
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(id.ToString()))!;
        Assert.True((await users.RemoveFromRoleAsync(user, "Admin")).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/orders")).StatusCode);
    }

    [SqlServerFact]
    public async Task LastAdmin_CannotRemoveOwnRoleOrDeactivateOwnAccount()
    {
        var id = await CreateUserAsync(admin: true);
        foreach (var change in new Action<ApplicationUser>[]
        {
            user => user.EmailConfirmed = false,
            user => { user.LockoutEnabled = true; user.LockoutEnd = DateTimeOffset.MaxValue; }
        })
        {
            var unavailableId = await CreateUserAsync(admin: true);
            using var scope = _app.Factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var unavailable = (await users.FindByIdAsync(unavailableId.ToString()))!;
            change(unavailable);
            Assert.True((await users.UpdateAsync(unavailable)).Succeeded);
        }
        using var client = await ClientAsync(id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/users/{id}/admin-role?isAdmin=false", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/orders")).StatusCode);
    }

    [SqlServerFact]
    public Task ConcurrentAdmins_CannotDemoteEachOther()
        => ConcurrentAdminChangesAsync("admin-role?isAdmin=false", self: false);

    [SqlServerFact]
    public Task ConcurrentAdmins_CannotDeactivateEachOther()
        => ConcurrentAdminChangesAsync("deactivate", self: false);

    [SqlServerFact]
    public Task ConcurrentSelfDeactivations_LeaveOneActiveAdmin()
        => ConcurrentAdminChangesAsync("", self: true);

    private async Task ConcurrentAdminChangesAsync(string action, bool self)
    {
        var first = await CreateUserAsync(admin: true);
        var second = await CreateUserAsync(admin: true);
        var gate = new ActionGate();
        await using var factory = _app.Factory.WithWebHostBuilder(webHostBuilder => webHostBuilder.ConfigureTestServices(services =>
            services.AddScoped<AccountSecurityMutationFilter>(_ => new CoordinatedAccountFilter(gate))));
        using var firstClient = await ClientAsync(first, factory);
        using var secondClient = await ClientAsync(second, factory);
        // Both requests pass initial JWT authentication before either enters the real SQL lock.
        var results = await Task.WhenAll(
            self ? firstClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })
                : firstClient.PostAsync($"/api/admin/users/{second}/{action}", null),
            self ? secondClient.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 1 })
                : secondClient.PostAsync($"/api/admin/users/{first}/{action}", null)
        );
        Assert.Single(results, result => result.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, result => result.StatusCode == (self ? HttpStatusCode.Conflict : HttpStatusCode.Unauthorized));
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admins = await users.GetUsersInRoleAsync("Admin");
        Assert.Single(admins, user => user.DeactivatedAtUtc is null && user.AnonymizedAtUtc is null);
        foreach (var result in results) result.Dispose();
    }

    private sealed class ActionGate
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
                _ready.TrySetResult();
            return _ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private sealed class CoordinatedAccountFilter(ActionGate gate) : AccountSecurityMutationFilter
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            await gate.ArriveAsync();
            await base.OnActionExecutionAsync(context, next);
        }
    }

    private async Task<HttpClient> ClientAsync(Guid userId, WebApplicationFactory<Program>? factory = null)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString()))!;
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var client = (factory ?? _app.Factory).CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
