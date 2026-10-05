using Biogarden.BusinessLogic.DTOs.Common;

namespace Biogarden.BusinessLogic.Services;

internal static class AdminPaging
{
    public static int Page(PagedQueryDto query, int count)
        => Math.Min(query.NormalizedPage, Math.Max(1, (int)Math.Ceiling((double)count / query.NormalizedPageSize)));
}
