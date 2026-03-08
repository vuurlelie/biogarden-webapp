using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Biogarden.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
}