using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;

namespace Biogarden.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task RegisterAsync(RegisterRequestDto registerRequestDto)
    {
        if (registerRequestDto.Password != registerRequestDto.ConfirmPassword)
        {
            throw new ArgumentException("A jelszavak nem egyeznek.");
        }

        var normalizedEmail = registerRequestDto.Email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "Ezzel az email címmel már létezik felhasználó.");
        }

        var user = new ApplicationUser
        {
            Email = normalizedEmail,
            UserName = normalizedEmail
        };

        var result = await _userManager.CreateAsync(
            user,
            registerRequestDto.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(error => error.Description));

            throw new InvalidOperationException(errors);
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto loginRequestDto)
    {
        var normalizedEmail = loginRequestDto.Email.Trim();

        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Hibás email vagy jelszó.");
        }

        if (user.RecordStatus == RecordStatus.Deleted
            || user.DeactivatedAtUtc is not null
            || user.AnonymizedAtUtc is not null)
        {
            throw new UnauthorizedAccessException("Ez a fiók jelenleg nem aktív.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(
            user,
            loginRequestDto.Password);

        if (!passwordValid)
        {
            throw new UnauthorizedAccessException("Hibás email vagy jelszó.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = await _tokenService.CreateTokenAsync(user);

        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = roles,
            Token = token
        };
    }
}
