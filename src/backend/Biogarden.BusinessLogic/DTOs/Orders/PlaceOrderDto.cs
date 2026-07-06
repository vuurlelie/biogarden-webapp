using Biogarden.DataAccess.Enums;

namespace Biogarden.BusinessLogic.DTOs.Orders;

public class PlaceOrderDto
{
    public int AddressId { get; set; }

    public FulfillmentMethod FulfillmentMethod { get; set; }
}
