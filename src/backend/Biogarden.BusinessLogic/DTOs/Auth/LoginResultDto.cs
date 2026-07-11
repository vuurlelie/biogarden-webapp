namespace Biogarden.BusinessLogic.DTOs.Auth;

public sealed class LoginResultDto
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public AuthResponseDto? AuthResponse { get; init; }

    public static LoginResultDto Success(AuthResponseDto authResponse)
    {
        return new LoginResultDto
        {
            Succeeded = true,
            AuthResponse = authResponse
        };
    }

    public static LoginResultDto Failure(string errorMessage)
    {
        return new LoginResultDto
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}
