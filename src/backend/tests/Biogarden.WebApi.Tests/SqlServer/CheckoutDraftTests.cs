using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Checkout;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Services;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class CheckoutDraftTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();
    private HttpClient Client()
    {
        var client = _app.Factory.CreateClient();
        return client;
    }
    private static string Path(Guid cart) => $"/api/checkout-drafts/{cart}";
    private static SaveCheckoutDraftDto Request(Guid? version = null, string name = "Draft customer")
        => new(version, new() { CustomerName = name, CustomerEmail = "draft@example.com" });
    private static async Task<CartDto> CartAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CartDto>("/api/cart"))!;

    [SqlServerFact]
    public async Task GuestDraft_RoundTripsWithoutEnteringCartResponse_AndCanBeDiscarded()
    {
        using var client = Client();
        var cart = await CartAsync(client);
        var request = Request();
        request.Fields.BillingTaxNumber = "12345678-1-12";
        using var saved = await client.PutAsJsonAsync(Path(cart.Id), request);
        saved.EnsureSuccessStatusCode();
        var draft = (await saved.Content.ReadFromJsonAsync<CheckoutDraftDto>())!;
        Assert.NotNull(draft.Version);
        Assert.True(draft.ExpiresAtUtc <= cart.ExpiresAtUtc);
        Assert.Equal("", draft.Fields!.BillingTaxNumber);
        using var read = await client.GetAsync(Path(cart.Id));
        Assert.True(read.Headers.CacheControl!.NoStore);
        Assert.Equal("Draft customer", (await read.Content.ReadFromJsonAsync<CheckoutDraftDto>())!.Fields!.CustomerName);
        Assert.DoesNotContain("draft@example.com", await client.GetStringAsync("/api/cart"));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Path(cart.Id)}?version={draft.Version}")).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<CheckoutDraftDto>(Path(cart.Id)))!.Fields);
    }

    [SqlServerFact]
    public async Task AnotherGuestCannotReadWriteOrDeleteByCartId()
    {
        using var owner = Client();
        using var other = Client();
        var cart = await CartAsync(owner);
        (await owner.PutAsJsonAsync(Path(cart.Id), Request())).EnsureSuccessStatusCode();
        await CartAsync(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Path(cart.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(Path(cart.Id), Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(Path(cart.Id))).StatusCode);
    }

    [SqlServerFact]
    public async Task RequestsRejectMissingCsrfHeaderInvalidSessionAndMalformedFields()
    {
        using var client = Client();
        var cart = await CartAsync(client);
        client.DefaultRequestHeaders.Remove("X-Checkout-Request");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Path(cart.Id), Request())).StatusCode);
        client.DefaultRequestHeaders.Add("X-Checkout-Request", "1");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Path(cart.Id), new { fields = new { customerName = 123 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Path(cart.Id), new { fields = new { password = "forbidden" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Path(cart.Id), Request(name: new string('x', 201)))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path(cart.Id))).StatusCode);
    }

    [SqlServerFact]
    public async Task CompetingTabsCannotOverwriteOrDiscardANewerVersion()
    {
        using var client = Client();
        var cart = await CartAsync(client);
        var responses = await Task.WhenAll(client.PutAsJsonAsync(Path(cart.Id), Request(name: "First")),
            client.PutAsJsonAsync(Path(cart.Id), Request(name: "Second")));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync(Path(cart.Id))).StatusCode);
        foreach (var response in responses) response.Dispose();
    }

    [SqlServerFact]
    public async Task ExpiredDraftIsNotRestoredAndCleanupRemovesPersonalData()
    {
        using var client = Client();
        var cart = await CartAsync(client);
        (await client.PutAsJsonAsync(Path(cart.Id), Request())).EnsureSuccessStatusCode();
        await using (var database = _app.OpenDatabase())
        {
            (await database.Carts.SingleAsync()).CheckoutDraftExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
            await database.SaveChangesAsync();
        }
        Assert.Null((await client.GetFromJsonAsync<CheckoutDraftDto>(Path(cart.Id)))!.Fields);
        using var scope = _app.Factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CheckoutDraftService>().PurgeExpiredAsync(default));
        await using var verification = _app.OpenDatabase();
        Assert.Null((await verification.Carts.SingleAsync()).CheckoutDraftJson);
    }

    [SqlServerFact]
    public async Task NewLoginDoesNotRestorePreviousSession_AndLogoutDeletesItsOwnDraft()
    {
        var userId = await CreateUserAsync();
        using var first = await AuthenticatedClientAsync(userId);
        var cart = await CartAsync(first);
        (await first.PutAsJsonAsync(Path(cart.Id), Request())).EnsureSuccessStatusCode();
        using var second = await AuthenticatedClientAsync(userId);
        Assert.NotEqual(first.DefaultRequestHeaders.Authorization, second.DefaultRequestHeaders.Authorization);
        Assert.Null((await second.GetFromJsonAsync<CheckoutDraftDto>(Path(cart.Id)))!.Fields);
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync("/api/checkout-drafts/session")).StatusCode);
        await using var database = _app.OpenDatabase();
        Assert.Null((await database.Carts.SingleAsync()).CheckoutDraftJson);
    }

    [SqlServerFact]
    public async Task LoginMergeDiscardsGuestDetailsAndPreservesUserDraft()
    {
        using var guest = Client();
        var guestCart = await CartAsync(guest);
        (await guest.PutAsJsonAsync(Path(guestCart.Id), Request(name: "Guest"))).EnsureSuccessStatusCode();
        using var user = await AuthenticatedClientAsync(await CreateUserAsync());
        var userCart = await CartAsync(user);
        (await user.PutAsJsonAsync(Path(userCart.Id), Request(name: "Customer"))).EnsureSuccessStatusCode();
        guest.DefaultRequestHeaders.Authorization = user.DefaultRequestHeaders.Authorization;
        (await guest.PostAsync("/api/cart/merge", null)).EnsureSuccessStatusCode();
        Assert.Equal("Customer", (await user.GetFromJsonAsync<CheckoutDraftDto>(Path(userCart.Id)))!.Fields!.CustomerName);
        await using var database = _app.OpenDatabase();
        Assert.Null((await database.Carts.SingleAsync(cart => cart.Id == guestCart.Id)).CheckoutDraftJson);
    }

    [SqlServerFact]
    public async Task OrderCommitClearsDraft_ButFailedOrderKeepsItForRetry()
    {
        using var client = Client();
        int productId;
        await using (var database = _app.OpenDatabase())
        {
            var product = new Product { Name = "Draft checkout product", Available = true, Price = 900m, StockQuantity = 1 };
            database.Products.Add(product);
            await database.SaveChangesAsync();
            productId = product.Id;
        }
        var added = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemDto { ProductId = productId, Quantity = 1 });
        var cart = (await added.Content.ReadFromJsonAsync<CartDto>())!;
        (await client.PutAsJsonAsync(Path(cart.Id), Request())).EnsureSuccessStatusCode();
        var request = new PlaceOrderDto
        {
            IdempotencyKey = cart.Id.ToString("N"),
            CustomerName = "Draft customer",
            CustomerEmail = "draft@example.com",
            CustomerPhone = "+361234567",
            AcceptedTermsVersion = LegalDocumentVersions.CurrentTermsVersion,
            AcceptedPrivacyPolicyVersion = LegalDocumentVersions.CurrentPrivacyPolicyVersion
        };
        await request.WithQuoteAsync(client);
        _app.QueueFailure.Enabled = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/orders", request)).StatusCode);
        Assert.NotNull((await client.GetFromJsonAsync<CheckoutDraftDto>(Path(cart.Id)))!.Fields);
        _app.QueueFailure.Enabled = false;
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/orders", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Path(cart.Id))).StatusCode);
        await using var verification = _app.OpenDatabase();
        Assert.Null((await verification.Carts.SingleAsync()).CheckoutDraftJson);
        Assert.Equal(1, await verification.Orders.CountAsync());
    }

    private async Task<Guid> CreateUserAsync()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "draft-user@example.com", Email = "draft-user@example.com", EmailConfirmed = true };
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, "Password123!");
        Assert.True(result.Succeeded);
        return user.Id;
    }
    private async Task<HttpClient> AuthenticatedClientAsync(Guid userId)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString()))!;
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
