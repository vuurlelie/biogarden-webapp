using Biogarden.BusinessLogic.DTOs.Posts;

using Biogarden.BusinessLogic.DTOs.Common;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IPostService
{
    Task<IReadOnlyList<PostListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResultDto<PostListItemDto>> GetPagedAsync(
        PostListQueryDto query,
        CancellationToken cancellationToken = default);
    Task<PagedResultDto<PostListItemDto>> GetAdminPagedAsync(
        PostListQueryDto query,
        CancellationToken cancellationToken = default);
    Task<PostDetailsDto?> GetDetailsAsync(int postId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(PostCreateDto newPost, CancellationToken cancellationToken = default);
    Task UpdateAsync(int postId, PostEditDto changes, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int postId, CancellationToken cancellationToken = default);
    Task<bool> IsImageInUseAsync(string? imagePath, CancellationToken cancellationToken = default);
}
