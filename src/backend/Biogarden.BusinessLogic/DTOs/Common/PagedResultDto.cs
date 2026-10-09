namespace Biogarden.BusinessLogic.DTOs.Common;

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}
