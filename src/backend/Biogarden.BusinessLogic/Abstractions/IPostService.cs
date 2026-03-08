using Biogarden.BusinessLogic.DTOs.Posts;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IPostService
{
    Task<IReadOnlyList<PostListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PostDetailsDto?> GetDetailsAsync(int postId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(PostCreateDto newPost, CancellationToken cancellationToken = default);
    Task UpdateAsync(int postId, PostEditDto changes, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int postId, CancellationToken cancellationToken = default);
}