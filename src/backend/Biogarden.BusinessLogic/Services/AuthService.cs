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

    public async Task RegisterAsync(RegisterRequestDto request)
    {
        if (request.Password != request.ConfirmPassword)
        {
            throw new ArgumentException("Passwords do not match.");
        }

        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new Exception(errors);
        }
    }
}