using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Posts;

public sealed class PostEditDto
{
    public string Title { get; set; } = string.Empty;
    public PostCategory Category { get; set; }
    public string Article { get; set; } = string.Empty;

    public string? Image { get; set; }

    public DateTime PublishedAt { get; set; }

    public bool Draft { get; set; }
}
