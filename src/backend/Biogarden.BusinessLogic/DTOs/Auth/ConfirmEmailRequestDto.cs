namespace Biogarden.BusinessLogic.DTOs.Auth;

public sealed class ConfirmEmailRequestDto
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;
}
