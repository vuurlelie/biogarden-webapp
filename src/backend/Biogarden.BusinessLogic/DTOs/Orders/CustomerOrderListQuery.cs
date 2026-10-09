using System.ComponentModel.DataAnnotations;
namespace Biogarden.BusinessLogic.DTOs.Orders;

public sealed class CustomerOrderListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 50)] public int PageSize { get; set; } = 10;
    [RegularExpression("^(recent|history|all)$")] public string View { get; set; } = "recent";
    [StringLength(32)] public string? Search { get; set; }
    [Range(2000, 9998)] public int? Year { get; set; }
}
