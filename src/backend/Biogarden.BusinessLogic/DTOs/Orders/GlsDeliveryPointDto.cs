using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public sealed record GlsDeliveryPointDto(string Id, FulfillmentMethod Type, string Name, string CountryCode, string PostalCode, string City, string Address, bool AcceptsCash, bool AcceptsCard);
