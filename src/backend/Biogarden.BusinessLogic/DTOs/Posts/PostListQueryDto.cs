using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Posts;

public sealed class PostListQueryDto : PagedQueryDto
{
    public PostCategory? Category { get; init; }

    public string? Sort { get; init; }
}
