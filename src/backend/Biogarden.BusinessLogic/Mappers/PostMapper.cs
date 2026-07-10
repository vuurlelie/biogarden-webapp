using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.DataAccess.Entities;

namespace Biogarden.BusinessLogic.Mappers;

public static class PostMapper
{
    public static PostListItemDto ToListItemDto(this Post post)
        => MapListItem(post);

    public static IReadOnlyList<PostListItemDto> ToListItemDtos(this IEnumerable<Post> posts)
        => posts
            .Select(MapListItem)
            .ToList();

    public static PostDetailsDto ToDetailsDto(this Post post)
        => new PostDetailsDto
        {
            Id = post.Id,
            Title = post.Title,
            Category = post.Category,
            Article = post.Article,
            Image = post.Image ?? BusinessRules.DefaultPostImagePath,
            PublishedAt = post.PublishedAt,
            Draft = post.Draft
        };

    public static Post ToEntity(this PostCreateDto dto)
    {
        return new Post
        {
            Title = dto.Title.Trim(),
            Category = dto.Category,
            Article = dto.Article.Trim(),
            Image = string.IsNullOrWhiteSpace(dto.Image) ? BusinessRules.DefaultPostImagePath : dto.Image,
            PublishedAt = dto.PublishedAt ?? DateTime.UtcNow,
            Draft = dto.Draft
        };
    }

    public static void ApplyChanges(this PostEditDto changes, Post entity)
    {
        entity.Title = changes.Title.Trim();
        entity.Category = changes.Category;
        entity.Article = changes.Article.Trim();

        if (changes.Image is not null)
        {
            entity.Image = string.IsNullOrWhiteSpace(changes.Image)
            ? BusinessRules.DefaultPostImagePath
            : changes.Image;
        }

        entity.PublishedAt = changes.PublishedAt;
        entity.Draft = changes.Draft;
    }

    private static PostListItemDto MapListItem(Post post)
        => new PostListItemDto
        {
            Id = post.Id,
            Title = post.Title,
            Category = post.Category,
            Article = post.Article,
            Image = post.Image ?? BusinessRules.DefaultPostImagePath,
            PublishedAt = post.PublishedAt,
            Draft = post.Draft
        };
}
