namespace Biogarden.BusinessLogic.DTOs.Common;

public abstract class PagedQueryDto
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 12;
    private const int MaximumPageSize = 60;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;

    public int NormalizedPage => Math.Max(DefaultPage, Page);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaximumPageSize);
}
