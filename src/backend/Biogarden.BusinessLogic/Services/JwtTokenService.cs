using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Biogarden.BusinessLogic.Services;

public class JwtTokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtSettings _jwtSettings;

    public JwtTokenService(UserManager<ApplicationUser> userManager, IOptions<JwtSettings> jwtOptions)
    {
        _userManager = userManager;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<string> CreateTokenAsync(ApplicationUser user)
    {
        var securityStamp = await _userManager.GetSecurityStampAsync(user);
        if (string.IsNullOrWhiteSpace(securityStamp))
            throw new InvalidOperationException("Cannot issue a session without a security stamp.");
        var userRoles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(AuthClaimTypes.SecurityStamp, securityStamp)
        };

        claims.AddRange(
            userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
