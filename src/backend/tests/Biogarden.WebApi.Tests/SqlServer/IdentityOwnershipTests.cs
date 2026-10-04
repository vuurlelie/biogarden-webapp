using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class IdentityOwnershipTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task ConcurrentRegistrationsForSameNormalizedEmailCreateOneAccountAndOneNotification()
    {
        _app.SaveGate.EntityType = typeof(ApplicationUser);
        using var first = _app.Factory.CreateClient();
        using var second = _app.Factory.CreateClient();
        var a = first.PostAsJsonAsync("/api/auth/register", new { email = "race@example.com", password = "Password123!", confirmPassword = "Password123!" });
        var b = second.PostAsJsonAsync("/api/auth/register", new { email = "RACE@example.com", password = "Password123!", confirmPassword = "Password123!" });
        try { await _app.SaveGate.WaitForBothAsync(); }
        finally { _app.SaveGate.Release(); }
        var responses = await Task.WhenAll(a, b);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        await using var db = _app.OpenDatabase();
        Assert.Single(await db.Users.ToListAsync());
        Assert.Single(await db.NotificationEmails.ToListAsync());
    }

    [SqlServerFact]
    public async Task AnotherCustomerCannotReadOrMutateOwnedCartOrOrder()
    {
        using var owner = await Client(await CreateUser());
        using var stranger = await Client(await CreateUser());
        using var guest = _app.Factory.CreateClient();
        var product = await Product();
        var cart = await Add(owner, product);
        var item = Assert.Single(cart.Items);
        foreach (var client in new[] { stranger, guest })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/cart/items/{item.Id}", new { quantity = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/cart/items/{item.Id}")).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<CartDto>("/api/cart"))!.Items);
        }
        Assert.Equal(1, (await owner.GetFromJsonAsync<CartDto>("/api/cart"))!.TotalItemCount);
        var request = await Request().WithQuoteAsync(owner);
        var placed = await owner.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = (await placed.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        foreach (var client in new[] { stranger, guest })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{order.Id}/receipt")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/checkout-recovery/{request.IdempotencyKey}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/orders/{order.Id}/sumup-checkout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/orders", request)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new { reason = "Unauthorized request" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/orders/{order.Id}/withdrawal-requests", new { reason = "Unauthorized request" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new { })).StatusCode);
        Assert.Empty((await stranger.GetFromJsonAsync<List<OrderDto>>("/api/orders"))!);
        await using var db = _app.OpenDatabase();
        Assert.Single(await db.Orders.ToListAsync());
        Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
        Assert.Equal(9, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Empty(await db.OrderWithdrawalRequests.ToListAsync());
        Assert.Empty(await db.PaymentAttempts.ToListAsync());
    }

    [SqlServerFact]
    public async Task SavedAddressesCannotBeSelectedByAnotherCustomerAtCheckout()
    {
        using var owner = await Client(await CreateUser());
        using var stranger = await Client(await CreateUser());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync("/api/profile/addresses", new
        {
            shippingAddress = new { postalCode = "1111", city = "Budapest", street = "Test street", houseNumber = "10" },
            useShippingAsBilling = true
        })).StatusCode);
        var addresses = (await owner.GetFromJsonAsync<UserAddressesDto>("/api/profile/addresses"))!;
        Assert.Null((await stranger.GetFromJsonAsync<UserAddressesDto>("/api/profile/addresses"))!.ShippingAddress);
        await Add(stranger, await Product());
        var request = Request();
        request.FulfillmentMethod = FulfillmentMethod.HomeDelivery;
        request.DeliveryAddressId = addresses.ShippingAddress!.Id;
        await request.WithQuoteAsync(stranger);
        Assert.Equal(HttpStatusCode.Conflict, (await stranger.PostAsJsonAsync("/api/orders", request)).StatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Equal(10, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Equal("Test street", (await owner.GetFromJsonAsync<UserAddressesDto>("/api/profile/addresses"))!.ShippingAddress!.Street);
    }

    [SqlServerFact]
    public async Task PublicPaymentStatusContainsOnlyItsLimitedContractAndWithdrawalRequiresMatchingLookup()
    {
        using var owner = _app.Factory.CreateClient();
        await Add(owner, await Product());
        var placed = await owner.PostAsJsonAsync("/api/orders", await Request().WithQuoteAsync(owner));
        placed.EnsureSuccessStatusCode();
        var order = (await placed.Content.ReadFromJsonAsync<OrderDto>())!;
        using var stranger = _app.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{order.Id}/payment-status?sumUpCheckoutReference=wrong")).StatusCode);
        using var status = await stranger.GetAsync($"/api/orders/{order.Id}/payment-status?sumUpCheckoutReference={order.Id:N}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        var json = await status.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(new[] { "confirmedAtUtc", "orderId", "orderNumber", "paymentStatus", "status" }, json.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync("/api/orders/withdrawal-requests", new
        {
            orderNumber = order.OrderNumber,
            customerEmail = "wrong@example.com",
            reason = "Please cancel the order."
        })).StatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Empty(await db.OrderWithdrawalRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task ResetTokenCannotBeReusedOrUsedForAnotherAccount()
    {
        var user = await CreateUser();
        var other = await CreateUser();
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        using var client = _app.Factory.CreateClient();
        object Reset(Guid id) => new { userId = id, token, password = "NewPassword123!", confirmPassword = "NewPassword123!" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", Reset(other.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password", Reset(user.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", Reset(user.Id))).StatusCode);
        await using var db = _app.OpenDatabase();
        Assert.Single(await db.NotificationEmails.ToListAsync());
        var unchanged = await db.Users.SingleAsync(x => x.Id == other.Id);
        Assert.Equal(other.SecurityStamp, unchanged.SecurityStamp);
        Assert.Equal(other.PasswordHash, unchanged.PasswordHash);
    }

    [SqlServerFact]
    public async Task ExpiredIdentityTokensAndExpiredOrTamperedJwtAreRejected()
    {
        var user = await CreateUser();
        // Exercise the real Identity provider's expiry path without sleeping or replacing validation.
        await using var expired = _app.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromSeconds(-1))));
        using var scope = expired.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        using var expiredClient = expired.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await expiredClient.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = user.Id,
            token,
            password = "NewPassword123!",
            confirmPassword = "NewPassword123!"
        })).StatusCode);
        var confirmationUser = await CreateUser(confirmed: false);
        var confirmation = await users.GenerateEmailConfirmationTokenAsync(confirmationUser);
        Assert.Equal(HttpStatusCode.BadRequest, (await expiredClient.PostAsJsonAsync("/api/auth/confirm-email", new { userId = confirmationUser.Id, token = confirmation })).StatusCode);
        var jwt = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var handler = new JwtSecurityTokenHandler();
        var original = handler.ReadJwtToken(jwt);
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        var oldJwt = handler.WriteToken(new JwtSecurityToken(settings.Issuer, settings.Audience, original.Claims,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)), SecurityAlgorithms.HmacSha256)));
        foreach (var invalid in new[] { oldJwt, jwt[..jwt.LastIndexOf('.')] + ".invalid-signature" })
        {
            expiredClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);
            Assert.Equal(HttpStatusCode.Unauthorized, (await expiredClient.GetAsync("/api/profile")).StatusCode);
        }
        await using var db = _app.OpenDatabase();
        Assert.Equal(user.PasswordHash, (await db.Users.SingleAsync(x => x.Id == user.Id)).PasswordHash);
        Assert.False((await db.Users.SingleAsync(x => x.Id == confirmationUser.Id)).EmailConfirmed);
        Assert.Empty(await db.NotificationEmails.ToListAsync());
    }

    private async Task<ApplicationUser> CreateUser(bool confirmed = true)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var email = $"ownership-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = confirmed };
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Password123!")).Succeeded);
        return user;
    }
    private async Task<HttpClient> Client(ApplicationUser user)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private async Task<int> Product()
    {
        await using var db = _app.OpenDatabase();
        var product = new Product { Name = "Ownership test product", Available = true, DeliveryAvailable = true, Price = 900m, StockQuantity = 10 };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }
    private static async Task<CartDto> Add(HttpClient client, int productId)
    {
        using var response = await client.PostAsJsonAsync("/api/cart/items", new { productId, quantity = 1 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CartDto>())!;
    }
    private static PlaceOrderDto Request() => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        CustomerName = "Test Customer",
        CustomerEmail = "customer@example.com",
        CustomerPhone = "+36301234567",
        FulfillmentMethod = FulfillmentMethod.PersonalPickup,
        AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
        AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
    };
}
