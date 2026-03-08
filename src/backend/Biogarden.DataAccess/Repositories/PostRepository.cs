using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
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