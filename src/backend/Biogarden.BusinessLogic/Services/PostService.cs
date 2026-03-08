using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;

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
        var entity = newPost.ToEntity();

        await _unitOfWork.PostRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateAsync(int postId, PostEditDto changes, CancellationToken cancellationToken = default)
    {
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
}