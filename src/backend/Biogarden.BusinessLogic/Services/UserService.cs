using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Biogarden.BusinessLogic.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppDbContext _databaseContext;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;

    public UserService(
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        IAppDbContext databaseContext,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _databaseContext = databaseContext;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return null;
        }

        return new UserProfileDto
        {
            Email = user.Email ?? string.Empty,
            CustomerType = user.CustomerType,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            CompanyName = user.CompanyName,
            TaxNumber = user.TaxNumber,
            PrintedInvoiceRequested = user.PrintedInvoiceRequested
        };
    }

    public async Task UpdateProfileAsync(
        Guid userId,
        UpdateUserProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            throw new KeyNotFoundException(
                $"User with id {userId} was not found.");
        }

        if (!Enum.IsDefined(dto.CustomerType))
        {
            throw new ArgumentException("Érvénytelen vásárlói típus.");
        }

        var companyName = NormalizeOptional(dto.CompanyName);
        var taxNumber = NormalizeOptional(dto.TaxNumber);

        if (dto.CustomerType == CustomerType.Company)
        {
            if (companyName is null || taxNumber is null)
            {
                throw new ArgumentException("Céges fiókhoz a cégnév és az adószám megadása kötelező.");
            }

            if (!IsValidTaxNumber(taxNumber))
            {
                throw new ArgumentException("Az adószám formátuma hibás. Példa: 12345678-1-12.");
            }
        }
        else
        {
            companyName = null;
            taxNumber = null;
            dto.PrintedInvoiceRequested = false;
            user.BillingAddressId = null;
        }

        user.CustomerType = dto.CustomerType;
        user.FirstName = NormalizeOptional(dto.FirstName);
        user.LastName = NormalizeOptional(dto.LastName);
        user.PhoneNumber = NormalizeOptional(dto.PhoneNumber);
        user.CompanyName = companyName;
        user.TaxNumber = taxNumber;
        user.PrintedInvoiceRequested = dto.PrintedInvoiceRequested;

        if (companyName != user.CompanyName && false)
            throw new ArgumentException("Céges számlázáshoz a cégnév és az adószám együttes megadása szükséges.");

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<UserAddressesDto?> GetAddressesAsync(Guid userId)
    {
        var user = await _userManager.Users
            .Include(u => u.ShippingAddress)
            .Include(u => u.BillingAddress)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return null;
        }

        return new UserAddressesDto
        {
            ShippingAddress = user.ShippingAddress?.ToDetailsDto(),
            BillingAddress = user.BillingAddress?.ToDetailsDto()
        };
    }

    public async Task UpdateAddressesAsync(
        Guid userId,
        UpdateUserAddressesDto dto)
    {
        var user = await _userManager.Users
            .Include(u => u.ShippingAddress)
            .Include(u => u.BillingAddress)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new KeyNotFoundException(
                $"User with id {userId} was not found.");
        }

        var previousShippingAddressId = user.ShippingAddressId;
        Address? shippingAddress = null;

        if (dto.ShippingAddress is null)
        {
            user.ShippingAddressId = null;
        }
        else
        {
            ValidateAddress(dto.ShippingAddress);
            shippingAddress = await UpsertAddressAsync(
                user.ShippingAddress,
                dto.ShippingAddress);

            await _unitOfWork.SaveChangesAsync();
            user.ShippingAddressId = shippingAddress.Id;
        }

        if (user.CustomerType == CustomerType.Private)
        {
            user.BillingAddressId = null;
        }
        else if (dto.UseShippingAsBilling)
        {
            user.BillingAddressId = shippingAddress?.Id;
        }
        else
        {
            if (dto.BillingAddress is null)
            {
                user.BillingAddressId = null;
            }
            else
            {
                ValidateAddress(dto.BillingAddress);

                var existingBillingAddress = user.BillingAddressId == previousShippingAddressId
                    ? null
                    : user.BillingAddress;
                var billingAddress = await UpsertAddressAsync(
                    existingBillingAddress,
                    dto.BillingAddress);

                await _unitOfWork.SaveChangesAsync();
                user.BillingAddressId = billingAddress.Id;
            }
        }

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    public async Task<AccountLifecycleResultDto> RequestAccountLifecycleAsync(
        Guid userId,
        AccountLifecycleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .Include(item => item.ShippingAddress)
            .Include(item => item.BillingAddress)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User with id {userId} was not found.");

        var orderCount = await _databaseContext.Orders
            .CountAsync(order => order.UserId == userId, cancellationToken);
        var activeOrderCount = await _databaseContext.Orders
            .CountAsync(order => order.UserId == userId && ActiveOrderStatuses.Contains(order.Status), cancellationToken);

        if (request.Type == AccountLifecycleRequestType.Deactivation)
        {
            await DeactivateUserAsync(user);
            await SendAccountDeactivatedEmailAsync(user.Email);
            await AddCompletedLifecycleRequestAsync(
                user.Id,
                request.Type,
                NormalizeOptional(request.Reason),
                activeOrderCount > 0
                    ? "A fiókodat deaktiváltuk. A folyamatban lévő rendelésedet továbbra is teljesítjük, de a fiókba már nem lehet bejelentkezni."
                    : "A fiókodat deaktiváltuk. A fiókba már nem lehet bejelentkezni, de kérésre újraaktiválható.",
                cancellationToken);

            return new AccountLifecycleResultDto
            {
                Message = activeOrderCount > 0
                    ? "A fiókodat deaktiváltuk. A folyamatban lévő rendelésedet továbbra is teljesítjük, de a fiókba már nem lehet bejelentkezni."
                    : "A fiókodat deaktiváltuk. A fiókba már nem lehet bejelentkezni, de kérésre újraaktiválható.",
                SessionEnded = true,
                Status = AccountLifecycleRequestStatus.Completed
            };
        }

        if (activeOrderCount > 0)
        {
            var message = "A fióktörlési kérelmedet rögzítettük. A folyamatban lévő rendelés lezárása után a fiók mentett személyes adatait töröljük vagy anonimizáljuk.";
            await AddPendingLifecycleRequestAsync(user.Id, request.Type, NormalizeOptional(request.Reason), message, cancellationToken);

            return new AccountLifecycleResultDto
            {
                Message = message,
                SessionEnded = false,
                Status = AccountLifecycleRequestStatus.Pending
            };
        }

        if (orderCount == 0)
        {
            var deletedEmail = user.Email;
            await DeleteUserWithoutOrdersAsync(user, cancellationToken);
            await SendAccountDeletedEmailAsync(deletedEmail);

            return new AccountLifecycleResultDto
            {
                Message = "A fiókodat és a mentett személyes adataidat töröltük.",
                SessionEnded = true,
                Status = AccountLifecycleRequestStatus.Completed
            };
        }

        var anonymizedEmail = user.Email;
        await AnonymizeUserAsync(user, cancellationToken);
        await SendAccountAnonymizedEmailAsync(anonymizedEmail);
        await AddCompletedLifecycleRequestAsync(
            user.Id,
            request.Type,
            NormalizeOptional(request.Reason),
            "A fiók mentett személyes adatait anonimizáltuk. A korábbi rendelésekhez tartozó, kötelezően megőrzendő számlázási és rendelési adatok változatlanul megmaradnak.",
            cancellationToken);

        return new AccountLifecycleResultDto
        {
            Message = "A fiók mentett személyes adatait anonimizáltuk. A korábbi rendelésekhez tartozó, kötelezően megőrzendő számlázási és rendelési adatok változatlanul megmaradnak.",
            SessionEnded = true,
            Status = AccountLifecycleRequestStatus.Completed
        };
    }

    internal static bool IsActiveOrderStatus(OrderStatus status) => ActiveOrderStatuses.Contains(status);

    internal static async Task DeactivateUserAsync(ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        user.RecordStatus = RecordStatus.Deleted;
        user.DeactivatedAtUtc ??= DateTime.UtcNow;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await UpdateIdentityUserAsync(userManager, user);
    }

    internal static async Task ReactivateUserAsync(ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        if (user.AnonymizedAtUtc is not null)
        {
            throw new InvalidOperationException("Anonimizált fiók nem aktiválható újra.");
        }

        user.RecordStatus = RecordStatus.Active;
        user.DeactivatedAtUtc = null;
        user.LockoutEnd = null;
        await UpdateIdentityUserAsync(userManager, user);
    }

    internal async Task AnonymizeUserAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var anonymizedEmail = $"deleted-user-{user.Id:N}@deleted.invalid";

        user.Email = anonymizedEmail;
        user.NormalizedEmail = anonymizedEmail.ToUpperInvariant();
        user.UserName = anonymizedEmail;
        user.NormalizedUserName = anonymizedEmail.ToUpperInvariant();
        user.FirstName = "Törölt";
        user.LastName = "Felhasználó";
        user.PhoneNumber = null;
        user.CustomerType = CustomerType.Private;
        user.CompanyName = null;
        user.TaxNumber = null;
        user.PrintedInvoiceRequested = false;
        user.EmailConfirmed = false;
        user.PhoneNumberConfirmed = false;
        user.RecordStatus = RecordStatus.Deleted;
        user.DeactivatedAtUtc ??= now;
        user.AnonymizedAtUtc = now;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        MarkAddressDeleted(user.ShippingAddress);
        MarkAddressDeleted(user.BillingAddress);
        user.ShippingAddressId = null;
        user.BillingAddressId = null;

        await DeleteOpenCartsAsync(user.Id, cancellationToken);
        await UpdateIdentityUserAsync(_userManager, user);
    }

    internal async Task DeleteUserWithoutOrdersAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await DeleteOpenCartsAsync(user.Id, cancellationToken);
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }
    }

    private async Task<Address> UpsertAddressAsync(
        Address? existingAddress,
        AddressCreateDto changes)
    {
        if (existingAddress is null)
        {
            var newAddress = changes.ToEntity();
            await _unitOfWork.AddressRepository.AddAsync(newAddress);
            return newAddress;
        }

        existingAddress.ApplyChanges(new AddressEditDto
        {
            PostalCode = changes.PostalCode,
            City = changes.City,
            Street = changes.Street,
            HouseNumber = changes.HouseNumber,
            Building = changes.Building,
            Floor = changes.Floor,
            Door = changes.Door,
            Doorbell = changes.Doorbell
        });
        _unitOfWork.AddressRepository.Update(existingAddress);

        return existingAddress;
    }

    private static void ValidateAddress(AddressCreateDto address)
    {
        if (string.IsNullOrWhiteSpace(address.PostalCode)
            || string.IsNullOrWhiteSpace(address.City)
            || string.IsNullOrWhiteSpace(address.Street)
            || string.IsNullOrWhiteSpace(address.HouseNumber))
        {
            throw new ArgumentException(
                "Az irányítószám, település, közterület és házszám megadása kötelező.");
        }

        if (!Regex.IsMatch(address.PostalCode.Trim(), @"^\d{4}$"))
        {
            throw new ArgumentException(
                "Az irányítószámnak 4 számjegyből kell állnia.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool IsValidTaxNumber(string taxNumber)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            taxNumber,
            @"^\d{8}(-\d-\d{2})?$");
    }

    private static readonly OrderStatus[] ActiveOrderStatuses =
    [
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.Shipped
    ];

    private async Task DeactivateUserAsync(ApplicationUser user)
    {
        await DeactivateUserAsync(user, _userManager);
    }

    private async Task AddPendingLifecycleRequestAsync(
        Guid userId,
        AccountLifecycleRequestType type,
        string? reason,
        string message,
        CancellationToken cancellationToken)
    {
        var existing = await _databaseContext.AccountLifecycleRequests
            .FirstOrDefaultAsync(
                request => request.UserId == userId
                    && request.Type == type
                    && request.Status == AccountLifecycleRequestStatus.Pending,
                cancellationToken);

        if (existing is not null)
        {
            existing.Reason = reason;
            existing.Message = message;
            await _databaseContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _databaseContext.AccountLifecycleRequests.Add(new AccountLifecycleRequest
        {
            UserId = userId,
            Type = type,
            Status = AccountLifecycleRequestStatus.Pending,
            Reason = reason,
            Message = message
        });
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddCompletedLifecycleRequestAsync(
        Guid userId,
        AccountLifecycleRequestType type,
        string? reason,
        string message,
        CancellationToken cancellationToken)
    {
        _databaseContext.AccountLifecycleRequests.Add(new AccountLifecycleRequest
        {
            UserId = userId,
            Type = type,
            Status = AccountLifecycleRequestStatus.Completed,
            Reason = reason,
            Message = message,
            HandledAtUtc = DateTime.UtcNow
        });
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteOpenCartsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var carts = await _databaseContext.Carts
            .Include(cart => cart.Items)
            .Where(cart => cart.UserId == userId && !cart.IsCheckedOut)
            .ToListAsync(cancellationToken);

        _databaseContext.Carts.RemoveRange(carts);
        await _databaseContext.SaveChangesAsync(cancellationToken);
    }

    internal async Task SendAccountAnonymizedEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = _emailTemplateService.BuildAccountAnonymizedMessage(email);
        await _emailService.SendAsync(message);
    }

    private async Task SendAccountDeactivatedEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = _emailTemplateService.BuildAccountDeactivatedMessage(email);
        await _emailService.SendAsync(message);
    }

    private async Task SendAccountDeletedEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = _emailTemplateService.BuildAccountDeletedMessage(email);
        await _emailService.SendAsync(message);
    }

    private static void MarkAddressDeleted(Address? address)
    {
        if (address is not null)
        {
            address.RecordStatus = RecordStatus.Deleted;
        }
    }

    private static async Task UpdateIdentityUserAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        await userManager.UpdateSecurityStampAsync(user);
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }
    }
}
