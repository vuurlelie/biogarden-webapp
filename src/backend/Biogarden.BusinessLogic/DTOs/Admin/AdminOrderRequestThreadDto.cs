namespace Biogarden.BusinessLogic.DTOs.Admin;

public sealed class AdminOrderRequestThreadDto
{
    public Guid OrderId { get; init; }
    public List<AdminOrderWithdrawalRequestDto> Messages { get; init; } = [];
}
