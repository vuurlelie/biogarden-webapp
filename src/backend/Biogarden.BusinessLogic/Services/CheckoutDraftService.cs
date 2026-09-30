using System.Text.Json;
using Biogarden.BusinessLogic.DTOs.Checkout;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.Services;

public sealed class CheckoutDraftService(IAppDbContext database, IUnitOfWork unitOfWork, TimeProvider clock, IGlsDeliveryPointDirectory gls)
{
    public async Task<CheckoutDraftDto> ReadAsync(Guid cartId, Guid? userId, string? guestId, string session, CancellationToken ct)
    {
        var cart = await FindOwnedCartAsync(cartId, userId, guestId, ct);
        var result = ToDto(cart, session);
        if (result.Fields?.GlsDeliveryPointId is not { } id || result.Fields.FulfillmentMethod is not { } method) return result;
        try { return result with { DeliveryPoint = await gls.ResolveAsync(id, method, ct) }; }
        catch (Exception ex) when (ex is InvalidOperationException or GlsDirectoryUnavailableException or ArgumentException)
        {
            // Retain personal edits; an unavailable destination needs reselection/retry, not draft loss.
            return result with { DeliveryPointError = ex.Message };
        }
    }

    public async Task<CheckoutDraftDto> SaveAsync(Guid cartId, Guid? userId, string? guestId, string session, SaveCheckoutDraftDto request, CancellationToken ct)
    {
        await using var ownerLock = await unitOfWork.CartRepository.AcquireOwnerLockAsync(userId, guestId, ct);
        var cart = await FindOwnedCartAsync(cartId, userId, guestId, ct);
        var current = ToDto(cart, session);
        if (current.Version != request.Version)
            throw new InvalidOperationException("A piszkozat megváltozott vagy lejárt. Töltsd be újra a folytatáshoz.");
        if (request.Fields.FulfillmentMethod is FulfillmentMethod.ParcelLocker or FulfillmentMethod.ParcelShop)
        {
            if (request.Fields.GlsDeliveryPointId is not null)
                await gls.ResolveAsync(request.Fields.GlsDeliveryPointId, request.Fields.FulfillmentMethod.Value, ct);
            request.Fields.DeliveryPostalCode = request.Fields.DeliveryCity = request.Fields.DeliveryStreet = request.Fields.DeliveryHouseNumber = "";
            request.Fields.DeliveryBuilding = request.Fields.DeliveryFloor = request.Fields.DeliveryDoor = request.Fields.DeliveryDoorbell = "";
        }
        else request.Fields.GlsDeliveryPointId = null;
        // Do not retain billing details for a private customer.
        if (request.Fields.CustomerType == 0)
        {
            request.Fields.BillingCompanyName = request.Fields.BillingTaxNumber = "";
            request.Fields.PostalCode = request.Fields.City = request.Fields.Street = request.Fields.HouseNumber = "";
            request.Fields.Building = request.Fields.Floor = request.Fields.Door = request.Fields.Doorbell = "";
            request.Fields.PrintedInvoiceRequested = false;
        }
        cart.CheckoutDraftJson = JsonSerializer.Serialize(request.Fields);
        cart.CheckoutDraftSession = session;
        cart.CheckoutDraftVersion = Guid.NewGuid();
        var deadline = clock.GetUtcNow().UtcDateTime.AddMinutes(30);
        cart.CheckoutDraftExpiresAtUtc = deadline < cart.ExpiresAtUtc ? deadline : cart.ExpiresAtUtc;
        await database.SaveChangesAsync(ct);
        return ToDto(cart, session);
    }

    public async Task DeleteAsync(Guid cartId, Guid? userId, string? guestId, string session, Guid? version, CancellationToken ct)
    {
        await using var ownerLock = await unitOfWork.CartRepository.AcquireOwnerLockAsync(userId, guestId, ct);
        var cart = await FindOwnedCartAsync(cartId, userId, guestId, ct);
        var current = ToDto(cart, session);
        if (current.Version != version)
            throw new InvalidOperationException("A piszkozat megváltozott. Töltsd be újra a törlés előtt.");
        if (cart.CheckoutDraftSession != session) return;
        cart.ClearCheckoutDraft();
        await database.SaveChangesAsync(ct);
    }

    public Task<int> ClearSessionAsync(Guid userId, string session, CancellationToken ct)
        => database.Carts.Where(cart => cart.UserId == userId && cart.CheckoutDraftSession == session)
            .ExecuteUpdateAsync(update => update
                .SetProperty(cart => cart.CheckoutDraftJson, (string?)null)
                .SetProperty(cart => cart.CheckoutDraftSession, (string?)null)
                .SetProperty(cart => cart.CheckoutDraftVersion, (Guid?)null)
                .SetProperty(cart => cart.CheckoutDraftExpiresAtUtc, (DateTime?)null), ct);

    public Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return database.Carts.Where(cart => cart.CheckoutDraftJson != null &&
            (cart.CheckoutDraftExpiresAtUtc <= now || cart.ExpiresAtUtc <= now || cart.IsCheckedOut))
            .OrderBy(cart => cart.CheckoutDraftExpiresAtUtc).Take(200)
            .ExecuteUpdateAsync(update => update
                .SetProperty(cart => cart.CheckoutDraftJson, (string?)null)
                .SetProperty(cart => cart.CheckoutDraftSession, (string?)null)
                .SetProperty(cart => cart.CheckoutDraftVersion, (Guid?)null)
                .SetProperty(cart => cart.CheckoutDraftExpiresAtUtc, (DateTime?)null), ct);
    }

    private async Task<Cart> FindOwnedCartAsync(Guid cartId, Guid? userId, string? guestId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await database.Carts.SingleOrDefaultAsync(cart => cart.Id == cartId && !cart.IsCheckedOut
            && cart.ExpiresAtUtc > now && (userId.HasValue ? cart.UserId == userId
                : cart.UserId == null && guestId != null && cart.SessionId == guestId), ct)
            ?? throw new KeyNotFoundException("A kosár nem található vagy lejárt.");
    }

    private CheckoutDraftDto ToDto(Cart cart, string session)
        => cart.CheckoutDraftSession == session && cart.CheckoutDraftExpiresAtUtc > clock.GetUtcNow().UtcDateTime
            && cart.CheckoutDraftJson is not null
                ? new(cart.CheckoutDraftVersion, cart.CheckoutDraftExpiresAtUtc, JsonSerializer.Deserialize<CheckoutDraftFields>(cart.CheckoutDraftJson))
                : new(null, null, null);
}
