using Biogarden.DataAccess.Enums;

namespace Biogarden.DataAccess.Entities;

public class Post
{
    public int Id { get; set; }

    public required string Title { get; set; }
    public PostCategory Category { get; set; }
    public required string Article { get; set; }

    public string Image { get; set; } = "/images/posts/default.png";

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public bool Draft { get; set; }

    public RecordStatus RecordStatus { get; set; } = RecordStatus.Active;
}