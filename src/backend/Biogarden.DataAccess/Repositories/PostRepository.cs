using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.DataAccess.Repositories;

public sealed class PostRepository : IPostRepository
{
    private readonly IAppDbContext _databaseContext;

    public PostRepository(IAppDbContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    public async Task<Post?> GetByIdAsync(int postId, CancellationToken cancellationToken = default)
    {
        return await _databaseContext.Posts
            .SingleOrDefaultAsync(post => post.Id == postId, cancellationToken);
    }

    public async Task<Post?> GetByIdIncludingDeletedAsync(int postId, CancellationToken cancellationToken = default)
    {
        return await _databaseContext.Posts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(post => post.Id == postId, cancellationToken);
    }

    public async Task<IReadOnlyList<Post>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _databaseContext.Posts
            .AsNoTracking()
            .OrderByDescending(post => post.PublishedAt)
            .ThenBy(post => post.Title)
            .ToListAsync(cancellationToken);

        return posts;
    }

    public async Task<IReadOnlyList<Post>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _databaseContext.Posts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(post => post.PublishedAt)
            .ThenBy(post => post.Title)
            .ToListAsync(cancellationToken);

        return posts;
    }

    public async Task<PagedResult<Post>> GetPagedAsync(
        string? search,
        PostCategory? category,
        bool publicOnly,
        bool? draft,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _databaseContext.Posts.AsNoTracking();

        if (publicOnly)
        {
            query = query.Where(post => !post.Draft);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(post =>
                post.Title.Contains(normalizedSearch) ||
                post.Article.Contains(normalizedSearch));
        }

        if (category.HasValue)
        {
            query = query.Where(post => post.Category == category.Value);
        }

        if (draft.HasValue)
        {
            query = query.Where(post => post.Draft == draft.Value);
        }

        query = sort switch
        {
            "oldest" => query.OrderBy(post => post.PublishedAt).ThenBy(post => post.Title),
            "abc" => query.OrderBy(post => post.Title).ThenByDescending(post => post.PublishedAt),
            _ => query.OrderByDescending(post => post.PublishedAt).ThenBy(post => post.Title)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Post>(items, page, pageSize, totalCount);
    }

    public async Task AddAsync(Post post, CancellationToken cancellationToken = default)
    {
        await _databaseContext.Posts.AddAsync(post, cancellationToken);
    }

    public void Update(Post post)
    {
        _databaseContext.Posts.Update(post);
    }

    public async Task<bool> DeleteByIdAsync(int postId, CancellationToken cancellationToken = default)
    {
        var post = await _databaseContext.Posts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(post => post.Id == postId, cancellationToken);

        if (post is null || post.RecordStatus == RecordStatus.Deleted)
        {
            return false;
        }

        post.RecordStatus = RecordStatus.Deleted;
        _databaseContext.Posts.Update(post);

        return true;
    }
}
