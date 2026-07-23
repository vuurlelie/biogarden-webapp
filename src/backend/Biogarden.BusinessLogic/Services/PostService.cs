using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.Services;

public sealed class PostService : IPostService
{
    private readonly IUnitOfWork _unitOfWork;

    public PostService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<PostListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _unitOfWork.PostRepository.GetAllAsync(cancellationToken);
        var items = posts.ToListItemDtos();
        return items;
    }

    public async Task<PagedResultDto<PostListItemDto>> GetPagedAsync(
        PostListQueryDto query,
        CancellationToken cancellationToken = default)
        => await GetPagedAsync(query, publicOnly: true, cancellationToken);

    public async Task<PagedResultDto<PostListItemDto>> GetAdminPagedAsync(
        PostListQueryDto query,
        CancellationToken cancellationToken = default)
        => await GetPagedAsync(query, publicOnly: false, cancellationToken);

    private async Task<PagedResultDto<PostListItemDto>> GetPagedAsync(
        PostListQueryDto query,
        bool publicOnly,
        CancellationToken cancellationToken)
    {
        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        var posts = await _unitOfWork.PostRepository.GetPagedAsync(
            query.Search,
            query.Category,
            publicOnly,
            query.Draft,
            query.Sort ?? string.Empty,
            page,
            pageSize,
            cancellationToken);

        return new PagedResultDto<PostListItemDto>(
            posts.Items.ToListItemDtos(),
            posts.Page,
            posts.PageSize,
            posts.TotalCount);
    }

    public async Task<PostDetailsDto?> GetDetailsAsync(int postId, CancellationToken cancellationToken = default)
    {
        var post = await _unitOfWork.PostRepository.GetByIdAsync(postId, cancellationToken);
        if (post is null)
        {
            return null;
        }

        var details = post.ToDetailsDto();
        return details;
    }

    public async Task<int> CreateAsync(PostCreateDto newPost, CancellationToken cancellationToken = default)
    {
        Validate(newPost.Title, newPost.Article);
        var entity = newPost.ToEntity();

        await _unitOfWork.PostRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateAsync(int postId, PostEditDto changes, CancellationToken cancellationToken = default)
    {
        Validate(changes.Title, changes.Article);
        var entity = await _unitOfWork.PostRepository.GetByIdAsync(postId, cancellationToken);
        if (entity is null)
        {
            throw new KeyNotFoundException($"Post {postId} not found.");
        }

        changes.ApplyChanges(entity);

        _unitOfWork.PostRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(int postId, CancellationToken cancellationToken = default)
    {
        var deleted = await _unitOfWork.PostRepository.DeleteByIdAsync(postId, cancellationToken);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return deleted;
    }

    public async Task<bool> IsImageInUseAsync(string? imagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath) ||
            string.Equals(imagePath, BusinessRules.DefaultPostImagePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var posts = await _unitOfWork.PostRepository.GetAllIncludingDeletedAsync(cancellationToken);
        return posts.Any(post =>
            post.RecordStatus == RecordStatus.Active &&
            string.Equals(post.Image, imagePath, StringComparison.OrdinalIgnoreCase));
    }

    private static void Validate(string title, string article)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 3)
        {
            throw new ArgumentException("A bejegyzés címe legalább 3 karakter legyen.");
        }

        if (string.IsNullOrWhiteSpace(article) || article.Trim().Length < 20)
        {
            throw new ArgumentException("A bejegyzés szövege legalább 20 karakter legyen.");
        }
    }
}
