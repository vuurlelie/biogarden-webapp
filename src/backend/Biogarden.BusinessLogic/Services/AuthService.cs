using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly AuthEmailOptions _authEmailOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOptions<AuthEmailOptions> authEmailOptions)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _authEmailOptions = authEmailOptions.Value;
    }

    public async Task RegisterAsync(RegisterRequestDto registerRequestDto)
    {
        await DeleteExpiredUnconfirmedUsersAsync();

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
            UserName = normalizedEmail,
            EmailConfirmed = false
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

        await SendEmailConfirmationAsync(user);
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

        if (!user.EmailConfirmed)
        {
            throw new UnauthorizedAccessException(
                "A fiók még nincs megerősítve. Kérjük, ellenőrizd az email fiókodat.");
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

    public async Task ConfirmEmailAsync(ConfirmEmailRequestDto confirmEmailRequestDto)
    {
        var user = await _userManager.FindByIdAsync(
            confirmEmailRequestDto.UserId.ToString());

        if (user is null
            || user.RecordStatus == RecordStatus.Deleted
            || user.AnonymizedAtUtc is not null)
        {
            throw new InvalidOperationException("Az email megerősítés nem sikerült.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        var result = await _userManager.ConfirmEmailAsync(
            user,
            confirmEmailRequestDto.Token);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Az email megerősítés nem sikerült.");
        }
    }

    public async Task ResendEmailConfirmationAsync(
        ResendEmailConfirmationRequestDto resendEmailConfirmationRequestDto)
    {
        var normalizedEmail = resendEmailConfirmationRequestDto.Email.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null
            || user.EmailConfirmed
            || user.RecordStatus == RecordStatus.Deleted
            || user.DeactivatedAtUtc is not null
            || user.AnonymizedAtUtc is not null)
        {
            return;
        }

        await SendEmailConfirmationAsync(user);
    }

    private async Task SendEmailConfirmationAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("A felhasználó email címe hiányzik.");
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationUrl = BuildEmailConfirmationUrl(user.Id, token);
        var message = _emailTemplateService.BuildEmailConfirmationMessage(
            user.Email,
            confirmationUrl);

        await _emailService.SendAsync(message);
    }

    private string BuildEmailConfirmationUrl(Guid userId, string token)
    {
        var clientBaseUrl = _authEmailOptions.ClientBaseUrl.TrimEnd('/');
        var confirmEmailPath = _authEmailOptions.ConfirmEmailPath.StartsWith('/')
            ? _authEmailOptions.ConfirmEmailPath
            : $"/{_authEmailOptions.ConfirmEmailPath}";

        return $"{clientBaseUrl}{confirmEmailPath}?userId={userId}&token={Uri.EscapeDataString(token)}";
    }

    private async Task DeleteExpiredUnconfirmedUsersAsync()
    {
        var retentionDays = Math.Max(
            1,
            _authEmailOptions.UnconfirmedAccountRetentionDays);

        var createdBefore = DateTime.UtcNow.AddDays(-retentionDays);

        var expiredUsers = await _userManager.Users
            .Where(user =>
                !user.EmailConfirmed
                && user.CreatedAt < createdBefore
                && user.RecordStatus == RecordStatus.Active
                && user.DeactivatedAtUtc == null
                && user.AnonymizedAtUtc == null)
            .ToListAsync();

        foreach (var expiredUser in expiredUsers)
        {
            await _userManager.DeleteAsync(expiredUser);
        }
    }
}
