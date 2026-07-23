namespace Biogarden.BusinessLogic.DTOs.Carts;

public class CartValidationIssueDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
