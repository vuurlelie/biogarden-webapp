using Biogarden.DataAccess.Entities;

using Biogarden.DataAccess.Enums;
using Biogarden.DataAccess.Models;

namespace Biogarden.DataAccess.Abstractions;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(int postId, CancellationToken cancellationToken = default);
    Task<Post?> GetByIdIncludingDeletedAsync(int postId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Post>> GetAllIncludingDeletedAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<Post>> GetPagedAsync(
        string? search,
        PostCategory? category,
        bool publicOnly,
        bool? draft,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    void Update(Post post);

    Task<bool> DeleteByIdAsync(int postId, CancellationToken cancellationToken = default);
}
