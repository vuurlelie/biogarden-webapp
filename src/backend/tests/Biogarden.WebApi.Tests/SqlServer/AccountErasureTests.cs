using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class AccountErasureTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task AnonymizationPreservesLegacyOrderAddressEvenWhenItWasAlsoTheSavedAddress()
    {
        var user = await SeedUser(true);
        await using (var db = _app.OpenDatabase())
        {
            (await db.Orders.SingleAsync()).AddressId = user.ShippingAddressId;
            await db.SaveChangesAsync();
        }
        await Erase(user, admin: false);
        await using var check = _app.OpenDatabase();
        var order = await check.Orders.Include(x => x.Address).SingleAsync();
        Assert.Equal(user.ShippingAddressId, order.AddressId);
        Assert.NotNull(order.Address);
        Assert.Equal("Saved street", order.Address.Street);
        Assert.Equal(RecordStatus.Active, order.Address.RecordStatus);
        Assert.Equal("Historical street", order.Street);
        Assert.Single(await check.Addresses.ToListAsync());
    }

    [SqlServerTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ErasureRemovesSavedAddressesAndOpenDraftsButPreservesOrderSnapshots(bool hasOrders, bool admin)
    {
        var user = await SeedUser(hasOrders);
        using var oldClient = await AuthenticatedClient(user);
        await Erase(user, admin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync("/api/profile")).StatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.Addresses.ToListAsync());
        Assert.Empty(await db.Carts.Where(x => !x.IsCheckedOut).ToListAsync());
        var stored = await db.Users.SingleOrDefaultAsync(x => x.Id == user.Id);
        if (hasOrders)
        {
            Assert.NotNull(stored);
            Assert.NotNull(stored.AnonymizedAtUtc);
            Assert.Null(stored.ShippingAddressId);
            Assert.Null(stored.BillingAddressId);
            Assert.Null(stored.PhoneNumber);
            Assert.NotEqual(user.Email, stored.Email);
            var order = await db.Orders.SingleAsync();
            Assert.Equal(user.Id, order.UserId);
            Assert.Equal("Historical customer", order.CustomerName);
            Assert.Equal("Historical street", order.Street);
            Assert.Equal("Historical delivery", order.DeliveryStreet);
            Assert.Equal("Historical company", order.BillingCompanyName);
            Assert.Equal(1800m, order.TotalPrice);
            Assert.Single(await db.Carts.ToListAsync());
        }
        else Assert.Null(stored);
        Assert.Single(await db.NotificationEmails.ToListAsync());
        Assert.Empty(_app.Sender.Sent);
    }

    [SqlServerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameShippingAndBillingAddressIsRemovedOnce(bool hasOrders)
    {
        var user = await SeedUser(hasOrders, sameAddress: true);
        await Erase(user, admin: false);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.Addresses.ToListAsync());
    }

    [SqlServerTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReferencedAddressesRemainUnchanged(bool hasOrders, bool orderReference)
    {
        var user = await SeedUser(hasOrders);
        var other = await SeedUser(false);
        await using (var db = _app.OpenDatabase())
        {
            if (orderReference)
                db.Orders.Add(NewOrder(other.Id, user.ShippingAddressId));
            else
                (await db.Users.SingleAsync(x => x.Id == other.Id)).BillingAddressId = user.ShippingAddressId;
            await db.SaveChangesAsync();
        }
        await Erase(user, admin: false);
        await using var check = _app.OpenDatabase();
        var address = await check.Addresses.SingleAsync(x => x.Id == user.ShippingAddressId);
        Assert.Equal("Saved street", address.Street);
        Assert.Equal("Test doorbell", address.Doorbell);
        Assert.Equal(RecordStatus.Active, address.RecordStatus);
        Assert.False(await check.Addresses.AnyAsync(x => x.Id == user.BillingAddressId));
        if (orderReference)
            Assert.Equal(address.Id, (await check.Orders.SingleAsync(x => x.UserId == other.Id)).AddressId);
        else
            Assert.Equal(address.Id, (await check.Users.SingleAsync(x => x.Id == other.Id)).BillingAddressId);
    }

    [SqlServerTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task QueueFailureRollsBackErasureAndAddressCleanup(bool hasOrders, bool admin)
    {
        var user = await SeedUser(hasOrders);
        _app.QueueFailure.NotificationsEnabled = true;
        if (admin)
        {
            using var scope = _app.Factory.Services.CreateScope();
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
                .GetRequiredService<IAdminUserService>().EraseAsync(user.Id, Guid.NewGuid()));
        }
        else
        {
            using var client = await AuthenticatedClient(user);
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).StatusCode);
        }
        await using (var db = _app.OpenDatabase())
        {
            var stored = await db.Users.SingleAsync(x => x.Id == user.Id);
            Assert.Equal(user.Email, stored.Email);
            Assert.Equal(user.SecurityStamp, stored.SecurityStamp);
            Assert.Null(stored.AnonymizedAtUtc);
            Assert.Equal(user.ShippingAddressId, stored.ShippingAddressId);
            Assert.Equal(user.BillingAddressId, stored.BillingAddressId);
            Assert.Equal(2, await db.Addresses.CountAsync());
            Assert.All(await db.Addresses.ToListAsync(), x => Assert.Equal(RecordStatus.Active, x.RecordStatus));
            Assert.Equal("synthetic draft", (await db.Carts.SingleAsync(x => !x.IsCheckedOut)).CheckoutDraftJson);
            Assert.Empty(await db.AccountLifecycleRequests.ToListAsync());
            Assert.Empty(await db.NotificationEmails.ToListAsync());
        }
        _app.QueueFailure.NotificationsEnabled = false;
        await Erase(user, admin);
        await using var check = _app.OpenDatabase();
        Assert.Empty(await check.Addresses.ToListAsync());
        Assert.Single(await check.NotificationEmails.ToListAsync());
    }

    [SqlServerFact]
    public async Task PendingErasureKeepsAddressesUntilOrderClosesAndAdminCompletesRequest()
    {
        var user = await SeedUser(true);
        await using (var db = _app.OpenDatabase())
        {
            (await db.Orders.SingleAsync()).Status = OrderStatus.Confirmed;
            await db.SaveChangesAsync();
        }
        using (var client = await AuthenticatedClient(user))
            Assert.Equal(HttpStatusCode.Accepted,
                (await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 })).StatusCode);
        int requestId;
        await using (var db = _app.OpenDatabase())
        {
            Assert.Equal(2, await db.Addresses.CountAsync());
            Assert.Null((await db.Users.SingleAsync()).AnonymizedAtUtc);
            var request = await db.AccountLifecycleRequests.SingleAsync();
            Assert.Equal(AccountLifecycleRequestStatus.Pending, request.Status);
            requestId = request.Id;
            (await db.Orders.SingleAsync()).Status = OrderStatus.Completed;
            await db.SaveChangesAsync();
        }
        using (var scope = _app.Factory.Services.CreateScope())
        {
            var admin = new ApplicationUser { Id = Guid.NewGuid(), UserName = "admin@example.com", Email = "admin@example.com" };
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(admin)).Succeeded);
            await scope.ServiceProvider.GetRequiredService<IAdminUserService>()
                .CompleteErasureRequestAsync(requestId, admin.Id, new AdminAccountLifecycleDecisionDto());
        }
        await using var check = _app.OpenDatabase();
        Assert.Empty(await check.Addresses.ToListAsync());
        Assert.NotNull((await check.Users.SingleAsync(x => x.Id == user.Id)).AnonymizedAtUtc);
        Assert.Equal(AccountLifecycleRequestStatus.Completed, (await check.AccountLifecycleRequests.SingleAsync()).Status);
        Assert.Equal("Historical street", (await check.Orders.SingleAsync()).Street);
    }

    private async Task<ApplicationUser> SeedUser(bool hasOrders, bool sameAddress = false)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var email = $"erasure-{Guid.NewGuid():N}@example.com";
        var address = NewAddress();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            PhoneNumber = "+36301234567",
            ShippingAddress = address,
            BillingAddress = sameAddress ? address : NewAddress()
        };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(user, "Password123!")).Succeeded);
        await using var db = _app.OpenDatabase();
        db.Carts.Add(new Cart { Id = Guid.NewGuid(), UserId = user.Id, CheckoutDraftJson = "synthetic draft" });
        if (hasOrders) db.Orders.Add(NewOrder(user.Id));
        await db.SaveChangesAsync();
        return user;
    }

    private static Address NewAddress() => new()
    {
        PostalCode = "8200",
        City = "Veszprém",
        Street = "Saved street",
        HouseNumber = "12",
        Building = "A",
        Floor = "2",
        Door = "3",
        Doorbell = "Test doorbell"
    };

    private static Order NewOrder(Guid userId, int? addressId = null) => new()
    {
        Id = Guid.NewGuid(),
        OrderNumber = $"ERASURE-{Guid.NewGuid():N}",
        UserId = userId,
        Cart = new Cart { Id = Guid.NewGuid(), UserId = userId, IsCheckedOut = true },
        AddressId = addressId,
        Status = OrderStatus.Completed,
        CustomerName = "Historical customer",
        Street = "Historical street",
        DeliveryStreet = "Historical delivery",
        BillingCompanyName = "Historical company",
        TotalPrice = 1800m
    };

    private async Task Erase(ApplicationUser user, bool admin)
    {
        if (admin)
        {
            using var scope = _app.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAdminUserService>().EraseAsync(user.Id, Guid.NewGuid());
        }
        else
        {
            using var client = await AuthenticatedClient(user);
            var response = await client.PostAsJsonAsync("/api/profile/account-lifecycle", new { type = 2 });
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
    }

    private async Task<HttpClient> AuthenticatedClient(ApplicationUser user)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var login = await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(
            new LoginRequestDto { Email = user.Email!, Password = "Password123!" });
        Assert.True(login.Succeeded);
        var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AuthResponse!.Token);
        return client;
    }
}
