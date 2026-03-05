using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Posts;

public sealed class PostListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public PostCategory Category { get; set; }
    public DateTime PublishedAt { get; set; }
    public bool Draft { get; set; }
}