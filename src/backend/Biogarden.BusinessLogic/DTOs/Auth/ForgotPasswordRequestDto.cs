using System.ComponentModel.DataAnnotations;

namespace Biogarden.BusinessLogic.DTOs.Auth;

public sealed class ForgotPasswordRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
