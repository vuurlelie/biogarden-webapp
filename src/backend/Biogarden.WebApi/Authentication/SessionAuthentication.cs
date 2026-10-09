using System.Security.Claims;
using Biogarden.BusinessLogic.Constants;
using Biogarden.DataAccess.Entities;
using Biogarden.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.WebApi.Authentication;

public static class SessionAuthentication
{
    public static async Task<bool> IsCurrentAsync(ClaimsPrincipal principal, IServiceProvider services, CancellationToken cancellationToken)
    {
        var stamp = principal.FindFirstValue(AuthClaimTypes.SecurityStamp);

        if (string.IsNullOrWhiteSpace(stamp) ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return false;
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        // Do not reuse an entity tracked earlier in this request: account state may have changed.
        var user = await users.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        var active = user is not null
            && user.RecordStatus == RecordStatus.Active
            && user.DeactivatedAtUtc is null
            && user.AnonymizedAtUtc is null
            && user.EmailConfirmed
            && !(user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
            && string.Equals(stamp, user.SecurityStamp, StringComparison.Ordinal);
        if (!active)
            return false;

        // Also reject tokens whose roles were changed outside the admin UI.
        var currentRoles = await users.GetRolesAsync(user!);
        return currentRoles.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value));
    }
}
