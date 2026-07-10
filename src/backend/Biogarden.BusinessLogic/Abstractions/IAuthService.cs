using Biogarden.BusinessLogic.DTOs.Auth;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto loginRequestDto);
    Task ConfirmEmailAsync(ConfirmEmailRequestDto confirmEmailRequestDto);
    Task ResendEmailConfirmationAsync(ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto);
}
