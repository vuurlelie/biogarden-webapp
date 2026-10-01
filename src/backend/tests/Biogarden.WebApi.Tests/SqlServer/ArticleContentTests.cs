using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biogarden.WebApi.Tests.SqlServer;

[Trait("Category", "SqlServer")]
public sealed class ArticleContentTests : IAsyncLifetime
{
    private readonly SqlServerApp _app = new();
    public Task InitializeAsync() => _app.InitializeAsync();
    public Task DisposeAsync() => _app.DisposeAsync();

    [SqlServerFact]
    public async Task AdminWritesAndPublicLegacyReadsAreSanitizedAndEmptyMarkupIsRejected()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "editor@example.com", Email = "editor@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "Password123!")).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Admin"))).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        using var admin = _app.Factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await scope.ServiceProvider.GetRequiredService<ITokenService>().CreateTokenAsync(user));
        const string safe = "<p>Fresh vegetables from our garden.</p>";
        const string unsafeHtml = safe + "<script>alert(1)</script>";
        using var created = await admin.PostAsJsonAsync("/api/admin/posts", new PostCreateDto { Title = "Harvest", Article = unsafeHtml });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = await created.Content.ReadFromJsonAsync<int>();
        await using var db = _app.OpenDatabase();
        Assert.Equal(safe, (await db.Posts.AsNoTracking().SingleAsync()).Article);

        using var updated = await admin.PutAsJsonAsync($"/api/admin/posts/{id}", new PostEditDto { Title = "Updated harvest", Article = unsafeHtml, PublishedAt = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(safe, (await db.Posts.AsNoTracking().SingleAsync()).Article);

        using var invalid = await admin.PutAsJsonAsync($"/api/admin/posts/{id}", new PostEditDto { Title = "Invalid", Article = "<p></p><p></p><p></p><p></p>" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("Updated harvest", (await db.Posts.AsNoTracking().SingleAsync()).Title);

        // Simulate a pre-release row without going through the sanitizing write path.
        await db.Posts.Where(p => p.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.Article, unsafeHtml));
        using var reader = _app.Factory.CreateClient();
        Assert.Equal(safe, (await reader.GetFromJsonAsync<PostDetailsDto>($"/api/posts/{id}"))!.Article);
        Assert.Equal(safe, Assert.Single((await reader.GetFromJsonAsync<PagedResultDto<PostListItemDto>>("/api/posts"))!.Items).Article);
        Assert.Equal(safe, Assert.Single((await admin.GetFromJsonAsync<PagedResultDto<PostListItemDto>>("/api/admin/posts"))!.Items).Article);
        Assert.Equal(unsafeHtml, (await db.Posts.AsNoTracking().SingleAsync()).Article);
    }
}
