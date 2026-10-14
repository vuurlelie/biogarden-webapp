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
        await using var databaseContext = _app.OpenDatabase();
        var timestamp = DateTime.UtcNow;
        databaseContext.Orders.AddRange(Enumerable.Range(1, 65).Select(index => NewOrder(index, timestamp)));
        await databaseContext.SaveChangesAsync();
        using var client = await Client();
        var first = await ReadPage<OrderDto>(client, "/api/admin/orders?pageSize=12");
        var second = await ReadPage<OrderDto>(client, "/api/admin/orders?page=2&pageSize=12");
        Assert.Equal(65, first.TotalCount);
        Assert.Equal(12, first.Items.Count);
        Assert.Empty(first.Items.Select(order => order.Id).Intersect(second.Items.Select(order => order.Id)));
        Assert.Equal(first.Items.Select(order => order.Id), (await ReadPage<OrderDto>(client, "/api/admin/orders?pageSize=12")).Items.Select(order => order.Id));
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
        await using var databaseContext = _app.OpenDatabase();
        var products = Enumerable.Range(1, 25).Select(index => new Product { Name = $"Paging {index:D2}", Price = 900, StockQuantity = 10, Available = true }).ToArray();
        databaseContext.Products.AddRange(products);
        await databaseContext.SaveChangesAsync();
        databaseContext.Carts.Add(new Cart { Id = Guid.NewGuid(), SessionId = "live", ExpiresAtUtc = DateTime.UtcNow.AddHours(1), Items = [new CartItem { ProductId = products[24].Id, Quantity = 8 }] });
        databaseContext.Carts.Add(new Cart { Id = Guid.NewGuid(), SessionId = "expired", ExpiresAtUtc = DateTime.UtcNow.AddHours(-1), Items = [new CartItem { ProductId = products[0].Id, Quantity = 10 }] });
        await databaseContext.SaveChangesAsync();
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
        await using var databaseContext = _app.OpenDatabase();
        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Admin", NormalizedName = "ADMIN" };
        databaseContext.Roles.Add(role);
        var users = Enumerable.Range(1, 25).Select(index => new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"paging{index}@example.com",
            Email = $"paging{index}@example.com",
            LastName = "Paging",
            FirstName = $"User {index:D2}",
            CreatedAt = new DateTime(2026, 1, 1),
            DeactivatedAtUtc = index == 24 ? DateTime.UtcNow : null,
            AnonymizedAtUtc = index == 25 ? DateTime.UtcNow : null
        }).ToArray();
        databaseContext.Users.AddRange(users);
        databaseContext.UserRoles.Add(new IdentityUserRole<Guid> { UserId = users[23].Id, RoleId = role.Id });
        databaseContext.AccountLifecycleRequests.Add(new AccountLifecycleRequest { UserId = users[22].Id, Type = AccountLifecycleRequestType.Erasure, Status = AccountLifecycleRequestStatus.Pending });
        await databaseContext.SaveChangesAsync();
        using var client = await Client();
        var first = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&pageSize=12");
        var second = await ReadPage<AdminUserDto>(client, "/api/admin/users?search=Paging&pageSize=12&page=2");
        Assert.Equal(24, first.TotalCount);
        Assert.Empty(first.Items.Select(order => order.Id).Intersect(second.Items.Select(order => order.Id)));
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
        await using var databaseContext = _app.OpenDatabase();
        var timestamp = DateTime.UtcNow;
        databaseContext.OrderWithdrawalRequests.AddRange(Enumerable.Range(1, 13).Select(index => new OrderWithdrawalRequest
        {
            Order = NewOrder(index, timestamp),
            CustomerName = "Paging Customer",
            CustomerEmail = "paging@example.com",
            Reason = $"Request {index}",
            RequestedAtUtc = timestamp
        }));
        await databaseContext.SaveChangesAsync();
        using var client = await Client();
        var last = await ReadPage<AdminOrderRequestThreadDto>(client, "/api/admin/orders/withdrawal-requests?status=1&page=2");
        var request = Assert.Single(Assert.Single(last.Items).Messages);
        Assert.Equal(13, last.TotalCount);
        var handleUrl = $"/api/admin/orders/withdrawal-requests/{request.Id}/handled";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(handleUrl, new { adminResponse = new string('x', 1001) })).StatusCode);
        (await client.PutAsJsonAsync(handleUrl, new { adminResponse = "  A kérésedet teljesítettük.  " })).EnsureSuccessStatusCode();
        var orderDetails = await client.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{request.OrderId}");
        Assert.Equal("A kérésedet teljesítettük.", orderDetails!.WithdrawalRequests.Single(withdrawalRequest => withdrawalRequest.Id == request.Id).AdminResponse);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(handleUrl, new { adminResponse = "Másik válasz" })).StatusCode);
        (await client.PutAsJsonAsync(handleUrl, new { adminResponse = "A kérésedet teljesítettük." })).EnsureSuccessStatusCode();
        var adjusted = await ReadPage<AdminOrderRequestThreadDto>(client, "/api/admin/orders/withdrawal-requests?status=1&page=2");
        Assert.Equal(1, adjusted.Page);
        Assert.Equal(12, adjusted.TotalCount);
        Assert.DoesNotContain(adjusted.Items, requestThread => requestThread.OrderId == request.OrderId);
        var handled = await ReadPage<AdminOrderRequestThreadDto>(client, $"/api/admin/orders/withdrawal-requests?status=2&search={request.OrderNumber}");
        Assert.Equal(request.Id, Assert.Single(Assert.Single(handled.Items).Messages).Id);
        databaseContext.OrderWithdrawalRequests.AddRange(Enumerable.Range(1, 2).Select(index => new OrderWithdrawalRequest
        {
            OrderId = request.OrderId,
            CustomerName = "Paging Customer",
            CustomerEmail = "paging@example.com",
            Reason = $"Additional context {index}",
            RequestedAtUtc = timestamp.AddMinutes(index)
        }));
        await databaseContext.SaveChangesAsync();
        var conversation = await ReadPage<AdminOrderRequestThreadDto>(client, "/api/admin/orders/withdrawal-requests?status=1&search=Additional%20context%202&pageSize=1");
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
        await using var databaseContext = _app.OpenDatabase();
        var userId = await databaseContext.Users.Select(user => user.Id).SingleAsync();
        var now = DateTime.UtcNow;
        var orders = Enumerable.Range(1, 25).Select(index => NewOrder(index, now.AddYears(-1))).ToArray();
        foreach (var order in orders) { order.UserId = userId; order.CompletedAtUtc = now.AddDays(-20); }
        orders[0].CompletedAtUtc = now.AddDays(-13);
        orders[1].Status = OrderStatus.Confirmed;
        orders[1].CompletedAtUtc = null;
        orders[2].WithdrawalRequests.Add(new OrderWithdrawalRequest { Reason = "Unresolved customer question", CustomerName = "Test", CustomerEmail = "test@example.com" });
        orders[3].CompletedAtUtc = null; // Historical completion date is unknown.
        databaseContext.Orders.AddRange(orders);
        await databaseContext.SaveChangesAsync();
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

    [SqlServerFact]
    public async Task CustomerOrderYearFilterUsesBudapestMidnightBoundaries()
    {
        using var owner = await Client(false);
        await using var databaseContext = _app.OpenDatabase();
        var userId = await databaseContext.Users.Select(user => user.Id).SingleAsync();
        // Midnight in Budapest is 23:00 UTC on the preceding day in January.
        var startUtc = new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc);
        var orders = new[]
        {
            NewOrder(1, startUtc.AddSeconds(-1)),
            NewOrder(2, startUtc),
            NewOrder(3, endUtc.AddSeconds(-1)),
            NewOrder(4, endUtc)
        };
        foreach (var order in orders)
        {
            order.UserId = userId;
        }
        databaseContext.Orders.AddRange(orders);
        await databaseContext.SaveChangesAsync();

        var filtered = await ReadPage<OrderDto>(owner, "/api/orders?view=all&year=2026");

        Assert.Equal(2, filtered.TotalCount);
        Assert.Equal(new[] { orders[2].Id, orders[1].Id }, filtered.Items.Select(order => order.Id));
        var previousYear = await ReadPage<OrderDto>(owner, "/api/orders?view=all&year=2025");
        var nextYear = await ReadPage<OrderDto>(owner, "/api/orders?view=all&year=2027");
        Assert.Equal(orders[0].Id, Assert.Single(previousYear.Items).Id);
        Assert.Equal(orders[3].Id, Assert.Single(nextYear.Items).Id);
    }

    [SqlServerTheory]
    [InlineData(1, 1)]
    [InlineData(7, 2)]
    public async Task DashboardCountsTodayFromBudapestMidnight(int month, int utcOffsetHours)
    {
        await using var databaseContext = _app.OpenDatabase();
        var midnightUtc = new DateTime(2026, month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-utcOffsetHours);
        databaseContext.Orders.AddRange(NewOrder(1, midnightUtc.AddSeconds(-1)), NewOrder(2, midnightUtc));
        await databaseContext.SaveChangesAsync();
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var clock = new DashboardClock(new DateTimeOffset(midnightUtc.AddMinutes(30)));
        var service = new Biogarden.BusinessLogic.Services.AdminDashboardService(databaseContext, users, clock);

        var dashboard = await service.GetAsync();

        Assert.Equal(1, dashboard.TodayOrderCount);
        Assert.Equal(2, dashboard.RecentOrders.Count);
    }

    [SqlServerFact]
    public async Task CataloguePagesBeyondTheEndRemainEmptyWithoutIntegerOverflow()
    {
        await using var databaseContext = _app.OpenDatabase();
        databaseContext.Products.Add(new Product { Name = "Pagination product", Price = 100, Available = true, StockQuantity = 5 });
        databaseContext.Posts.Add(new Post { Title = "Pagination post", Article = "A published article for the paging test.", PublishedAt = DateTime.UtcNow });
        await databaseContext.SaveChangesAsync();
        using var client = _app.Factory.CreateClient();

        foreach (var path in new[] { "/api/products", "/api/posts" })
        {
            var first = await ReadPage<System.Text.Json.JsonElement>(client, path);
            Assert.Single(first.Items);
            foreach (var page in new[] { 2, int.MaxValue, 1_073_741_825 })
            {
                // 1,073,741,824 * 60 wraps to zero with 32-bit arithmetic.
                var beyondEnd = await ReadPage<System.Text.Json.JsonElement>(client, $"{path}?page={page}&pageSize=60");
                Assert.Empty(beyondEnd.Items);
                Assert.Equal(1, beyondEnd.TotalCount);
                Assert.Equal(page, beyondEnd.Page);
            }
        }
    }

    [SqlServerFact]
    public async Task DashboardIncludesPendingRequestsOlderThanSevenDays()
    {
        await using var databaseContext = _app.OpenDatabase();
        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var order = NewOrder(1, now.AddDays(-30));
        databaseContext.Orders.Add(order);
        databaseContext.OrderWithdrawalRequests.AddRange(
            new OrderWithdrawalRequest { Order = order, CustomerName = "Old pending", CustomerEmail = "uat@example.com", Reason = "Test", RequestedAtUtc = now.AddDays(-20) },
            new OrderWithdrawalRequest { Order = order, CustomerName = "Recent pending", CustomerEmail = "uat@example.com", Reason = "Test", RequestedAtUtc = now.AddDays(-1) });
        await databaseContext.SaveChangesAsync();
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = new Biogarden.BusinessLogic.Services.AdminDashboardService(databaseContext, users, new DashboardClock(new DateTimeOffset(now)));

        var dashboard = await service.GetAsync();

        Assert.Equal(2, dashboard.PendingWithdrawalRequestCount);
        Assert.Equal(new[] { "Recent pending", "Old pending" }, dashboard.PendingWithdrawalRequests.Select(request => request.CustomerName));
    }

    private sealed class DashboardClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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
            if (!await roles.RoleExistsAsync("Admin"))
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        }
        var token = await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user);
        var client = _app.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
