namespace Biogarden.BusinessLogic.DTOs.Auth;

public sealed class ResetPasswordRequestDto
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
