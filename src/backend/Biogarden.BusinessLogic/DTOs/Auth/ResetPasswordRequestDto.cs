using System.ComponentModel.DataAnnotations;

namespace Biogarden.BusinessLogic.DTOs.Auth;

public sealed class ResetPasswordRequestDto
{
    public Guid UserId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
