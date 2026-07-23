using Biogarden.DataAccess.Enums;

public sealed class PostListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public PostCategory Category { get; set; }
    public string Article { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public bool Draft { get; set; }
}
