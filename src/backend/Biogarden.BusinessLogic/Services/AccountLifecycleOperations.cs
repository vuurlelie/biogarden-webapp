using Biogarden.BusinessLogic.Abstractions;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

// Callers own authorization, lifecycle decisions and the transaction that also persists
// pending/completed requests. These operations share that scoped context and queue notifications.
public sealed class AccountLifecycleOperations(
    UserManager<ApplicationUser> userManager,
    IAppDbContext databaseContext,
    IAccountNotificationQueue notifications,
    IEmailTemplateService emailTemplateService
) : IAccountLifecycleOperations
{
    public async Task DeactivateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.RecordStatus = RecordStatus.Deleted;
        user.DeactivatedAtUtc ??= DateTime.UtcNow;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        await UpdateIdentityUserAsync(userManager, user);
        await QueueAccountDeactivatedEmailAsync(user.Email, cancellationToken);
    }

    public async Task ReactivateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (user.AnonymizedAtUtc is not null)
        {
            throw new InvalidOperationException("Anonimizált fiók nem aktiválható újra.");
        }

        user.RecordStatus = RecordStatus.Active;
        user.DeactivatedAtUtc = null;
        user.LockoutEnd = null;
        await UpdateIdentityUserAsync(userManager, user);
        await QueueAccountReactivatedEmailAsync(user.Email, cancellationToken);
    }

    public async Task AnonymizeAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var email = user.Email;
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

        var addressIds = GetSavedAddressIds(user);
        user.ShippingAddress = null;
        user.BillingAddress = null;
        user.ShippingAddressId = null;
        user.BillingAddressId = null;

        await DeleteOpenCartsAsync(user.Id, cancellationToken);
        await UpdateIdentityUserAsync(userManager, user);
        await DeleteUnreferencedAddressesAsync(addressIds, cancellationToken);
        await QueueAccountAnonymizedEmailAsync(email, cancellationToken);
    }

    public async Task DeleteWithoutOrdersAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var email = user.Email;
        var addressIds = GetSavedAddressIds(user);
        await DeleteOpenCartsAsync(user.Id, cancellationToken);
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }
        await DeleteUnreferencedAddressesAsync(addressIds, cancellationToken);
        await QueueAccountDeletedEmailAsync(email, cancellationToken);
    }

    private async Task DeleteOpenCartsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var carts = await databaseContext.Carts
            .Include(cart => cart.Items)
            .Where(cart => cart.UserId == userId && !cart.IsCheckedOut)
            .ToListAsync(cancellationToken);

        databaseContext.Carts.RemoveRange(carts);
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task QueueAccountAnonymizedEmailAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = emailTemplateService.BuildAccountAnonymizedMessage(email);
        await notifications.QueueMessageAsync(message, "account-anonymized", cancellationToken);
    }

    private async Task QueueAccountDeactivatedEmailAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = emailTemplateService.BuildAccountDeactivatedMessage(email);
        await notifications.QueueMessageAsync(message, "account-deactivated", cancellationToken);
    }

    private async Task QueueAccountDeletedEmailAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = emailTemplateService.BuildAccountDeletedMessage(email);
        await notifications.QueueMessageAsync(message, "account-deleted", cancellationToken);
    }

    private async Task QueueAccountReactivatedEmailAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var message = emailTemplateService.BuildAccountReactivatedMessage(email);
        await notifications.QueueMessageAsync(message, "account-reactivated", cancellationToken);
    }

    private static int[] GetSavedAddressIds(ApplicationUser user)
        => new[] { user.ShippingAddressId, user.BillingAddressId }
            .OfType<int>().Distinct().ToArray();

    private async Task DeleteUnreferencedAddressesAsync(int[] addressIds, CancellationToken cancellationToken)
    {
        if (addressIds.Length == 0)
            return;

        // Run after Identity has persisted the detached/deleted user, inside the caller's
        // transaction. Historical order addresses and other accounts' addresses must survive.
        var addresses = await databaseContext.Addresses
            .Where(address => addressIds.Contains(address.Id)
                && !userManager.Users.Any(user => user.ShippingAddressId == address.Id || user.BillingAddressId == address.Id)
                && !databaseContext.Orders.Any(order => order.AddressId == address.Id))
            .ToListAsync(cancellationToken);
        databaseContext.Addresses.RemoveRange(addresses);
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpdateIdentityUserAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        // Persist account changes and revocation together; do not ignore a failed stamp update.
        user.SecurityStamp = Guid.NewGuid().ToString();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
        }
    }
}
