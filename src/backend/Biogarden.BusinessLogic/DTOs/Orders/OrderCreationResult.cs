namespace Biogarden.BusinessLogic.DTOs.Orders;

public sealed record OrderCreationResult(OrderDto Order, bool Created);
