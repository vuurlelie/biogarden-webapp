using Biogarden.DataAccess.Abstractions;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _databaseContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IAccountNotificationQueue _notifications;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly AuthEmailOptions _authEmailOptions;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IAccountNotificationQueue notifications,
        IEmailTemplateService emailTemplateService,
        IOptions<AuthEmailOptions> authEmailOptions,
        IAppDbContext databaseContext)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _notifications = notifications;
        _databaseContext = databaseContext;
        _emailTemplateService = emailTemplateService;
        _authEmailOptions = authEmailOptions.Value;
    }

    public async Task<AuthOperationResultDto> RegisterAsync(RegisterRequestDto registerRequestDto)
    {
        try
        {
            return await NotificationTransaction.RunAsync(_databaseContext, () => RegisterAsyncCore(registerRequestDto));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another registration can win after our initial lookup. The transaction has
            // rolled back; only translate the conflict if that account now exists.
            if (await _userManager.FindByEmailAsync(registerRequestDto.Email.Trim()) is not null)
                return AuthOperationResultDto.Failure("Ezzel az email címmel már létezik felhasználó.");
            throw;
        }
    }

    private async Task<AuthOperationResultDto> RegisterAsyncCore(RegisterRequestDto registerRequestDto)
    {
        await DeleteExpiredUnconfirmedUsersAsync();

        if (registerRequestDto.Password != registerRequestDto.ConfirmPassword)
        {
            return AuthOperationResultDto.Failure("A jelszavak nem egyeznek.");
        }

        if (!Enum.IsDefined(registerRequestDto.CustomerType))
        {
            return AuthOperationResultDto.Failure("Érvénytelen vásárlói típus.");
        }

        var companyName = NormalizeOptional(registerRequestDto.CompanyName);
        var taxNumber = NormalizeOptional(registerRequestDto.TaxNumber);
        AddressCreateDto? billingAddress = null;

        if (registerRequestDto.CustomerType == CustomerType.Company)
        {
            if (companyName is null || taxNumber is null)
            {
                return AuthOperationResultDto.Failure(
                    "Céges fiókhoz a cégnév és az adószám megadása kötelező.");
            }

            if (registerRequestDto.BillingAddress is null ||
                !IsCompleteAddress(registerRequestDto.BillingAddress))
            {
                return AuthOperationResultDto.Failure(
                    "Céges fiókhoz a számlázási cím megadása kötelező.");
            }

            billingAddress = registerRequestDto.BillingAddress;

            if (!IsValidTaxNumber(taxNumber))
            {
                return AuthOperationResultDto.Failure(
                    "Az adószám formátuma hibás. Példa: 12345678-1-12.");
            }
        }
        else
        {
            companyName = null;
            taxNumber = null;
            registerRequestDto.PrintedInvoiceRequested = false;
        }

        var normalizedEmail = registerRequestDto.Email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            return AuthOperationResultDto.Failure(
                "Ezzel az email címmel már létezik felhasználó.");
        }

        var user = new ApplicationUser
        {
            Email = normalizedEmail,
            UserName = normalizedEmail,
            EmailConfirmed = false,
            CustomerType = registerRequestDto.CustomerType,
            CompanyName = companyName,
            TaxNumber = taxNumber,
            BillingAddress = billingAddress?.ToEntity(),
            PrintedInvoiceRequested = registerRequestDto.PrintedInvoiceRequested
        };

        var result = await _userManager.CreateAsync(
            user,
            registerRequestDto.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(error => error.Description));

            return AuthOperationResultDto.Failure(errors);
        }

        await QueueEmailConfirmationAsync(user);

        return AuthOperationResultDto.Success();
    }

    public async Task<LoginResultDto> LoginAsync(LoginRequestDto loginRequestDto)
    {
        var normalizedEmail = loginRequestDto.Email.Trim();

        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            return LoginResultDto.Failure("Hibás email vagy jelszó.");
        }

        if (user.RecordStatus == RecordStatus.Deleted
            || user.DeactivatedAtUtc is not null
            || user.AnonymizedAtUtc is not null
            || (user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow))
        {
            return LoginResultDto.Failure("Ez a fiók jelenleg nem aktív.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(
            user,
            loginRequestDto.Password);

        if (!passwordValid)
        {
            return LoginResultDto.Failure("Hibás email vagy jelszó.");
        }

        if (!user.EmailConfirmed)
        {
            return LoginResultDto.Failure(
                "A fiók még nincs megerősítve. Kérjük, ellenőrizd az email fiókodat.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = await _tokenService.CreateTokenAsync(user);

        return LoginResultDto.Success(
            new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles,
                Token = token
            });
    }

    public async Task<AuthOperationResultDto> ConfirmEmailAsync(ConfirmEmailRequestDto confirmEmailRequestDto)
    {
        var user = await _userManager.FindByIdAsync(
            confirmEmailRequestDto.UserId.ToString());

        if (user is null
            || user.RecordStatus == RecordStatus.Deleted
            || user.AnonymizedAtUtc is not null)
        {
            return AuthOperationResultDto.Failure("Az email megerősítés nem sikerült.");
        }

        if (user.EmailConfirmed)
        {
            return AuthOperationResultDto.Success();
        }

        var result = await _userManager.ConfirmEmailAsync(
            user,
            confirmEmailRequestDto.Token);

        if (!result.Succeeded)
        {
            var refreshedUser = await _userManager.FindByIdAsync(
                confirmEmailRequestDto.UserId.ToString());

            if (refreshedUser?.EmailConfirmed == true)
            {
                return AuthOperationResultDto.Success();
            }

            return AuthOperationResultDto.Failure("Az email megerősítés nem sikerült.");
        }

        return AuthOperationResultDto.Success();
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

        await QueueEmailConfirmationAsync(user);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequestDto forgotPasswordRequestDto)
    {
        var normalizedEmail = forgotPasswordRequestDto.Email.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null
            || !user.EmailConfirmed
            || user.RecordStatus == RecordStatus.Deleted
            || user.DeactivatedAtUtc is not null
            || user.AnonymizedAtUtc is not null
            || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        await _notifications.QueueAuthenticationAsync(user, AccountNotificationQueue.PasswordReset);
    }

    public Task<AuthOperationResultDto> ResetPasswordAsync(ResetPasswordRequestDto resetPasswordRequestDto)
        => NotificationTransaction.RunAsync(_databaseContext, () => ResetPasswordAsyncCore(resetPasswordRequestDto));

    private async Task<AuthOperationResultDto> ResetPasswordAsyncCore(
        ResetPasswordRequestDto resetPasswordRequestDto)
    {
        if (resetPasswordRequestDto.Password != resetPasswordRequestDto.ConfirmPassword)
        {
            return AuthOperationResultDto.Failure("A jelszavak nem egyeznek.");
        }

        var user = await _userManager.FindByIdAsync(
            resetPasswordRequestDto.UserId.ToString());

        if (user is null
            || user.RecordStatus == RecordStatus.Deleted
            || user.DeactivatedAtUtc is not null
            || user.AnonymizedAtUtc is not null)
        {
            return AuthOperationResultDto.Failure("A jelszó visszaállítása nem sikerült.");
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            resetPasswordRequestDto.Token,
            resetPasswordRequestDto.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(error => error.Description));

            return AuthOperationResultDto.Failure(
                string.IsNullOrWhiteSpace(errors)
                    ? "A jelszó visszaállítása nem sikerült."
                    : errors);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var message = _emailTemplateService.BuildPasswordChangedMessage(user.Email);
            await _notifications.QueueMessageAsync(message, "password-changed");
        }

        return AuthOperationResultDto.Success();
    }

    private Task QueueEmailConfirmationAsync(ApplicationUser user)
        => _notifications.QueueAuthenticationAsync(user, AccountNotificationQueue.Confirmation);

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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsValidTaxNumber(string taxNumber)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            taxNumber,
            @"^\d{8}(-\d-\d{2})?$");
    }

    private static bool IsCompleteAddress(AddressCreateDto address)
    {
        return !string.IsNullOrWhiteSpace(address.PostalCode)
            && !string.IsNullOrWhiteSpace(address.City)
            && !string.IsNullOrWhiteSpace(address.Street)
            && !string.IsNullOrWhiteSpace(address.HouseNumber);
    }
}
