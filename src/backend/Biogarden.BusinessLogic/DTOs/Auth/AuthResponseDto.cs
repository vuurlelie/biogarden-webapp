namespace Biogarden.BusinessLogic.DTOs.Auth;

public class AuthResponseDto
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public IEnumerable<string> Roles { get; set; } = [];

    public string Token { get; set; } = string.Empty;
}
