using Biogarden.DataAccess.Entities;

namespace Biogarden.DataAccess.Abstractions;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(int postId, CancellationToken cancellationToken = default);
    Task<Post?> GetByIdIncludingDeletedAsync(int postId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    void Update(Post post);

    Task<bool> DeleteByIdAsync(int postId, CancellationToken cancellationToken = default);
}