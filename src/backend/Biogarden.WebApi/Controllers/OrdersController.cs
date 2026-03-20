using System.Security.Claims;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> CreateOrderFromCart(
        [FromBody] PlaceOrderDto placeOrderDto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var sessionId = GetCartSessionId();

        var order = await _orderService.CreateOrderFromCartAsync(
            userId,
            sessionId,
            placeOrderDto,
            cancellationToken);

        return Ok(order);
    }

    [HttpPost("{orderId:guid}/confirm-payment")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> ConfirmOrderPayment(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var order = await _orderService.ConfirmOrderPaymentAsync(
            userId,
            orderId,
            cancellationToken);

        return Ok(order);
    }

    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> CancelOrder(
        Guid orderId,
        [FromBody] CancelOrderDto cancelOrderDto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var order = await _orderService.CancelOrderAsync(
            userId,
            orderId,
            cancelOrderDto,
            cancellationToken);

        return Ok(order);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var orders = await _orderService.GetMyOrdersAsync(
            userId,
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> GetOrderById(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var order = await _orderService.GetOrderByIdAsync(
            userId,
            orderId,
            cancellationToken);

        return Ok(order);
    }

    private Guid GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("Authenticated user id is missing or invalid.");
        }

        return userId;
    }

    private string? GetCartSessionId()
    {
        const string cartCookieName = "biogarden_cart_id";

        if (Request.Cookies.TryGetValue(cartCookieName, out var cartSessionId))
        {
            return cartSessionId;
        }

        return null;
    }
}