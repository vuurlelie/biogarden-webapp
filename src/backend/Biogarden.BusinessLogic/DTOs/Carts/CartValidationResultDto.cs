namespace Biogarden.BusinessLogic.DTOs.Carts;

public class CartValidationResultDto
{
    public bool IsValid { get; set; }

    public List<CartValidationIssueDto> Issues { get; set; } = [];
}
