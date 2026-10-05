using Biogarden.BusinessLogic.DTOs.Common;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Admin;

public abstract class AdminListQuery : PagedQueryDto
{
    public string? Search { get; init; }
}

public sealed class AdminOrderListQuery : AdminListQuery
{
    public OrderStatus? Status { get; init; }
    public FulfillmentMethod? Fulfillment { get; init; }
}

public sealed class AdminUserListQuery : AdminListQuery
{
    public string? Role { get; init; }
    public string? Status { get; init; }
}

public sealed class AdminInventoryListQuery : AdminListQuery
{
    public string? Stock { get; init; }
}

public sealed class AdminWithdrawalListQuery : AdminListQuery
{
    public OrderWithdrawalRequestStatus? Status { get; init; }
}
