using Biogarden.BusinessLogic.DTOs.Auth;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequestDto request);
}