using Biogarden.BusinessLogic.DTOs.Auth;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAuthService
{
    Task<AuthOperationResultDto> RegisterAsync(RegisterRequestDto request);
    Task<LoginResultDto> LoginAsync(LoginRequestDto loginRequestDto);
    Task<AuthOperationResultDto> ConfirmEmailAsync(ConfirmEmailRequestDto confirmEmailRequestDto);
    Task ResendEmailConfirmationAsync(ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto);
}
