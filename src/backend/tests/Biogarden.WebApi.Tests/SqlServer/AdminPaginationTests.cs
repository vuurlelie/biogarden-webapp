using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class AdminPaginationTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task OrdersAreFilteredBeforePagingWithStableTiesAndBoundedSizes()
    {
        await using var db = _app.OpenDatabase();
        var timestamp = DateTime.UtcNow;
        db.Orders.AddRange(Enumerable.Range(1, 65).Select(i => NewOrder(i, timestamp)));
        await db.SaveChangesAsync();
        using var client = await Client();
        var first = await ReadPage<OrderDto>(client, "/api/admin/orders?pageSize=12");
        var second = await ReadPage<OrderDto>(client, "/api/admin/orders?page=2&pageSize=12");
        Assert.Equal(65, first.TotalCount);
        Assert.Equal(12, first.Items.Count);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        Assert.Equal(first.Items.Select(x => x.Id), (await ReadPage<OrderDto>(client, "/api/admin/orders?pageSize=12")).Items.Select(x => x.Id));
        var filtered = await ReadPage<OrderDto>(client, "/api/admin/orders?search=ORD-65&status=3&fulfillment=0");
        Assert.Equal("ORD-65", Assert.Single(filtered.Items).OrderNumber);
        Assert.Equal(1, filtered.TotalCount);
        var capped = await ReadPage<OrderDto>(client, "/api/admin/orders?pageSize=2147483647");
        Assert.Equal(60, capped.PageSize);
        Assert.Equal(60, capped.Items.Count);
        var last = await ReadPage<OrderDto>(client, "/api/admin/orders?page=2147483647&pageSize=60");
        Assert.Equal(2, last.Page);
        Assert.Equal(5, last.Items.Count);
        var normalized = await ReadPage<OrderDto>(client, "/api/admin/orders?page=-1&pageSize=0");
        Assert.Equal(1, normalized.Page);
        Assert.Single(normalized.Items);
        var detail = await client.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{last.Items[0].Id}");
        Assert.Equal(last.Items[0].Version, detail!.Version);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/orders/{Guid.NewGuid()}")).StatusCode);
    }

    [SqlServerFact]
    public async Task InventoryFiltersCountLiveReservationsBeforePagingAndSupportsSingleRead()
    {
        await using var db = _app.OpenDatabase();
        var products = Enumerable.Range(1, 25).Select(i => new Product { Name = $"Paging {i:D2}", Price = 900, StockQuantity = 10, Available = true }).ToArray();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        db.Carts.Add(new Cart { Id = Guid.NewGuid(), SessionId = "live", ExpiresAtUtc = DateTime.UtcNow.AddHours(1), Items = [new CartItem { ProductId = products[24].Id, Quantity = 8 }] });
        db.Carts.Add(new Cart { Id = Guid.NewGuid(), SessionId = "expired", ExpiresAtUtc = DateTime.UtcNow.AddHours(-1), Items = [new CartItem { ProductId = products[0].Id, Quantity = 10 }] });
        await db.SaveChangesAsync();
        using var client = await Client();
        var low = await ReadPage<AdminInventoryItemDto>(client, "/api/admin/inventory?stock=low&pageSize=1");
        var item = Assert.Single(low.Items);
        Assert.Equal(products[24].Id, item.ProductId);
        Assert.Equal(8, item.ReservedQuantity);
        Assert.Equal(2, item.AvailableQuantity);
        Assert.Equal(1, low.TotalCount);
        var reserved = await ReadPage<AdminInventoryItemDto>(client, "/api/admin/inventory?stock=reserved");
        Assert.Single(reserved.Items);
        var empty = await ReadPage<AdminInventoryItemDto>(client, "/api/admin/inventory?stock=empty");
        Assert.Equal(0, empty.TotalCount);
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.TotalPages);
        var search = await ReadPage<AdminInventoryItemDto>(client, "/api/admin/inventory?search=Paging%2025");
        Assert.Equal(item.ProductId, Assert.Single(search.Items).ProductId);
        var detail = await client.GetFromJsonAsync<AdminInventoryItemDto>($"/api/admin/inventory/{item.ProductId}");
        Assert.Equal(item.Version, detail!.Version);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/inventory/2147483647")).StatusCode);
    }

    [SqlServerFact]
    public async Task AccountsFilterRolesLifecycleAndPendingRequestsBeforePaging()
    {
        await using var db = _app.OpenDatabase();
        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
        db.Roles.Add(role);
        var users = Enumerable.Range(1, 25).Select(i => new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"paging{i}@example.com",
            Email = $"paging{i}@example.com",
            LastName = "Paging",
            FirstName = $"User {i:D2}",
            CreatedAt = new DateTime(2026, 1, 1),
            DeactivatedAtUtc = i == 24 ? DateTime.UtcNow : null,
            AnonymizedAtUtc = i == 25 ? DateTime.UtcNow : null
        }).ToArray();
        db.Users.AddRange(users);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = users[23].Id, RoleId = role.Id });
        db.AccountLifecycleRequests.Add(new AccountLifecycleRequest { UserId = users[22].Id, Type = AccountLifecycleRequestType.Erasure, Status = AccountLifecycleRequestStatus.Pending });
        await db.SaveChangesAsync();
        using var client = await Client();
        var first = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&pageSize=12");
        var second = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&pageSize=12&page=2");
        Assert.Equal(24, first.TotalCount);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
        var admins = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&role=admin&status=inactive");
        Assert.True(Assert.Single(admins.Items).IsAdmin);
        Assert.Equal(users[23].Id, admins.Items[0].Id);
        var pending = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&status=request&role=customer");
        Assert.Equal(users[22].Id, Assert.Single(pending.Items).Id);
        Assert.NotNull(pending.Items[0].PendingRequest);
        var active = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&status=active");
        Assert.Equal(23, active.TotalCount);
    }

    [SqlServerFact]
    public async Task WithdrawalPagesShrinkAfterHandlingAndKeepFilteredCounts()
    {
        await using var db = _app.OpenDatabase();
        var timestamp = DateTime.UtcNow;
        db.OrderWithdrawalRequests.AddRange(Enumerable.Range(1, 13).Select(i => new OrderWithdrawalRequest
        {
            Order = NewOrder(i, timestamp),
            CustomerName = "Paging Customer",
            CustomerEmail = "paging@example.com",
            Reason = $"Request {i}",
            RequestedAtUtc = timestamp
        }));
        await db.SaveChangesAsync();
        using var client = await Client();
        var last = await ReadPage<AdminOrderRequestThreadDto>(client, "/api/admin/orders/withdrawal-requests?status=1&page=2");
        var request = Assert.Single(Assert.Single(last.Items).Messages);
        Assert.Equal(13, last.TotalCount);
        (await client.PutAsync($"/api/admin/orders/withdrawal-requests/{request.Id}/handled", null)).EnsureSuccessStatusCode();
        var adjusted = await ReadPage<AdminOrderRequestThreadDto>(client, "/api/admin/orders/withdrawal-requests?status=1&page=2");
        Assert.Equal(1, adjusted.Page);
        Assert.Equal(12, adjusted.TotalCount);
        Assert.DoesNotContain(adjusted.Items, x => x.OrderId == request.OrderId);
        var handled = await ReadPage<AdminOrderRequestThreadDto>(client, $"/api/admin/orders/withdrawal-requests?status=2&search={request.OrderNumber}");
        Assert.Equal(request.Id, Assert.Single(Assert.Single(handled.Items).Messages).Id);
        db.OrderWithdrawalRequests.AddRange(Enumerable.Range(1, 2).Select(i => new OrderWithdrawalRequest
        {
            OrderId = request.OrderId,
            CustomerName = "Paging Customer",
            CustomerEmail = "paging@example.com",
            Reason = $"Additional context {i}",
            RequestedAtUtc = timestamp.AddMinutes(i)
        }));
        await db.SaveChangesAsync();
        var conversation = await ReadPage<AdminOrderRequestThreadDto>(client,
            "/api/admin/orders/withdrawal-requests?status=1&search=Additional%20context%202&pageSize=1");
        Assert.Equal(1, conversation.TotalCount);
        var messages = Assert.Single(conversation.Items).Messages;
        Assert.Equal(3, messages.Count);
        Assert.Equal(request.Id, messages[0].Id);
        Assert.Equal(OrderWithdrawalRequestStatus.Handled, messages[0].Status);
        Assert.Equal("Additional context 2", messages[2].Reason);
    }

    [SqlServerFact]
    public async Task ListsAndSingleReadsRequireAdminAndRejectInvalidFilters()
    {
        using var anonymous = _app.Factory.CreateClient();
        using var customer = await Client(false);
        using var admin = await Client();
        foreach (var path in new[] { "orders", "users", "inventory", "orders/withdrawal-requests", $"orders/{Guid.NewGuid()}", "inventory/1" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/admin/{path}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/admin/{path}")).StatusCode);
        }
        foreach (var path in new[] { "orders?status=999", "orders?fulfillment=999", "users?role=unknown", "users?status=unknown", "inventory?stock=unknown", "orders/withdrawal-requests?status=999", "orders?search=" + new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/admin/{path}")).StatusCode);
    }

    [SqlServerFact]
    public async Task CustomerHistoryIsPagedOwnedAndKeepsOpenRequestsRecent()
    {
        using var owner = await Client(false);
        await using var db = _app.OpenDatabase();
        var userId = await db.Users.Select(user => user.Id).SingleAsync();
        var now = DateTime.UtcNow;
        var orders = Enumerable.Range(1, 25).Select(i => NewOrder(i, now.AddYears(-1))).ToArray();
        foreach (var order in orders) { order.UserId = userId; order.CompletedAtUtc = now.AddDays(-20); }
        orders[0].CompletedAtUtc = now.AddDays(-13);
        orders[1].Status = OrderStatus.Confirmed;
        orders[1].CompletedAtUtc = null;
        orders[2].WithdrawalRequests.Add(new OrderWithdrawalRequest { Reason = "Unresolved customer question", CustomerName = "Test", CustomerEmail = "test@example.com" });
        orders[3].CompletedAtUtc = null; // Historical completion date is unknown.
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
        var recent = await ReadPage<OrderDto>(owner, "/api/orders");
        Assert.Equal(3, recent.TotalCount);
        var first = await ReadPage<OrderDto>(owner, "/api/orders?view=history");
        var second = await ReadPage<OrderDto>(owner, "/api/orders?view=history&page=2");
        Assert.Equal(22, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Empty(first.Items.Select(order => order.Id).Intersect(second.Items.Select(order => order.Id)));
        var filtered = await ReadPage<OrderDto>(owner, $"/api/orders?view=history&search={orders[3].OrderNumber}&year={now.AddYears(-1).Year}");
        Assert.Equal(orders[3].Id, Assert.Single(filtered.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/orders/{orders[3].Id}")).StatusCode);
        using var stranger = await Client(false);
        Assert.Empty((await ReadPage<OrderDto>(stranger, "/api/orders?view=all")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/orders/{orders[3].Id}")).StatusCode);
        foreach (var query in new[] { "page=0", "pageSize=1000", "view=unknown", "year=9999" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/api/orders?{query}")).StatusCode);
    }

    private static Order NewOrder(int number, DateTime timestamp) => new()
    {
        Id = Guid.NewGuid(),
        OrderNumber = $"ORD-{number:D2}",
        CreatedAtUtc = timestamp,
        CustomerName = "Paging Customer",
        CustomerEmail = "paging@example.com",
        Status = OrderStatus.Completed,
        Cart = new Cart { Id = Guid.NewGuid(), SessionId = Guid.NewGuid().ToString(), IsCheckedOut = true }
    };

    private static async Task<PagedResultDto<T>> ReadPage<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PagedResultDto<T>>())!;
    }

    private async Task<HttpClient> Client(bool admin = true)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"operator-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        if (admin)
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Admin")) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
