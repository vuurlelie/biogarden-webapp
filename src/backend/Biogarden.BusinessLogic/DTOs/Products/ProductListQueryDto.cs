using Biogarden.BusinessLogic.DTOs.Common;

namespace Biogarden.BusinessLogic.DTOs.Products;

public sealed class ProductListQueryDto : PagedQueryDto
{
    public string? Search { get; init; }

    public string? Category { get; init; }

    public string? Sort { get; init; }

    public bool AvailableOnly { get; init; }

    public bool UnavailableOnly { get; init; }

    public bool DeliveryOnly { get; init; }
}
