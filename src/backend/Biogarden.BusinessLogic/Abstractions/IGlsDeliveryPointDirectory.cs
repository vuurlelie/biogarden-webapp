using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.Abstractions;

public interface IGlsDeliveryPointDirectory
{
    Task<GlsDeliveryPointDto> ResolveAsync(string id, FulfillmentMethod type, CancellationToken cancellationToken = default);
}

public sealed class GlsDirectoryUnavailableException : Exception
{
    public GlsDirectoryUnavailableException() : base("A GLS átvételi pontok ellenőrzése most nem elérhető. Próbáld újra később, vagy válassz másik átvételi módot.") { }
}
