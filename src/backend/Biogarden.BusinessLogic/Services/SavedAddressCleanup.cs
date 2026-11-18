using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

internal static class SavedAddressCleanup
{
    // Call after persisting the detached/deleted user, inside the same transaction.
    public static async Task DeleteUnreferencedAsync(IAppDbContext database, IQueryable<ApplicationUser> users,
        int[] addressIds, CancellationToken cancellationToken)
    {
        if (addressIds.Length == 0)
            return;
        var addresses = await database.Addresses
            .Where(address => addressIds.Contains(address.Id)
                && !users.Any(user => user.ShippingAddressId == address.Id || user.BillingAddressId == address.Id)
                && !database.Orders.Any(order => order.AddressId == address.Id))
            .ToListAsync(cancellationToken);
        database.Addresses.RemoveRange(addresses);
        await database.SaveChangesAsync(cancellationToken);
    }
}
