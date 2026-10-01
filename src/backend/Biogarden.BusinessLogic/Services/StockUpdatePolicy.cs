using Biogarden.DataAccess.Abstractions;

namespace Biogarden.BusinessLogic.Services;

internal static class StockUpdatePolicy
{
    // Load the product before querying reservations. Cart mutations advance its rowversion,
    // so a reservation added after this check conflicts with the subsequent product save.
    public static async Task<int> ValidateAsync(IUnitOfWork unitOfWork, Product product, int quantity, CancellationToken cancellationToken)
    {
        var reserved = await unitOfWork.CartRepository.GetActiveReservedQuantityForProductAsync(product.Id, null, cancellationToken);
        if (quantity < 0 || quantity < reserved)
            throw new InvalidOperationException($"A készlet nem lehet kevesebb a jelenleg lefoglalt {reserved} darabnál.");
        return reserved;
    }
}
