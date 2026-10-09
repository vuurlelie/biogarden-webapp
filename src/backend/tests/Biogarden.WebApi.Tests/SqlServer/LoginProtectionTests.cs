using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class LoginProtectionTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();
    private const string Email = "login-security@example.com";
    private const string Password = "ValidPassword123!";

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedAttempts_LockAccount_AndExpiredLockAllowsRecovery(bool concurrent)
    {
        await CreateUser();
        if (concurrent)
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Login("WrongPassword123!")));
        else
            for (var index = 0; index < 5; index++) Assert.False((await Login("WrongPassword123!")).Succeeded);
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync(Email))!;
            Assert.True(await users.IsLockedOutAsync(user));
            Assert.InRange(user.LockoutEnd!.Value, DateTimeOffset.UtcNow.AddMinutes(4), DateTimeOffset.UtcNow.AddMinutes(6));
            Assert.False((await Login(Password)).Succeeded);
            Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        }
        Assert.True((await Login(Password)).Succeeded);
        using var check = _app.Factory.Services.CreateScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(0, (await manager.FindByEmailAsync(Email))!.AccessFailedCount);
    }

    [SqlServerFact]
    public async Task SuccessfulLogin_ResetsFailureCount_AndFailuresHaveNeutralMessages()
    {
        await CreateUser();
        var missing = await Login(Password, "missing@example.com");
        var wrong = await Login("WrongPassword123!");
        Assert.Equal(missing.ErrorMessage, wrong.ErrorMessage);
        Assert.True((await Login(Password)).Succeeded);
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(Email))!;
        Assert.Equal(0, user.AccessFailedCount);
        user.DeactivatedAtUtc = DateTime.UtcNow;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.Equal(missing.ErrorMessage, (await Login(Password)).ErrorMessage);
    }

    private async Task CreateUser()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.CreateAsync(new ApplicationUser { UserName = Email, Email = Email, EmailConfirmed = true }, Password)).Succeeded);
    }

    private async Task<LoginResultDto> Login(string password, string email = Email)
    {
        using var scope = _app.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(new() { Email = email, Password = password });
    }
}
