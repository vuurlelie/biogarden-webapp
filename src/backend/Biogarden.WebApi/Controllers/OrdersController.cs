using System.Security.Claims;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Payments;
using Biogarden.WebApi.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Biogarden.WebApi.Controllers;

[ApiController]
[Biogarden.WebApi.Filters.BrowserSessionRequest]
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
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PublicForm)]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderDto>> CreateOrderFromCart(
        [FromBody] PlaceOrderDto placeOrderDto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserIdOrNull();
        var sessionId = GetCartSessionId();

        var result = await _orderService.CreateOrderFromCartAsync(
            userId,
            sessionId,
            placeOrderDto,
            cancellationToken);

        return result.Created
            ? CreatedAtAction(nameof(GetOrderById), new { orderId = result.Order.Id }, result.Order)
            : Ok(result.Order);
    }

    [HttpGet("{orderId:guid}/receipt")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<OrderDto>> GetCheckoutOrder(Guid orderId, CancellationToken cancellationToken)
        => Ok(await _orderService.GetCheckoutOrderAsync(GetCurrentUserIdOrNull(), GetCartSessionId(), orderId, cancellationToken));

    [HttpGet("checkout-recovery/{idempotencyKey}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Payment)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> RecoverCheckoutOrder(string idempotencyKey, CancellationToken cancellationToken)
        => Ok(await _orderService.RecoverCheckoutOrderAsync(GetCurrentUserIdOrNull(), GetCartSessionId(), idempotencyKey, cancellationToken));

    [HttpGet("checkout-methods")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ActionResult<IReadOnlyList<CheckoutMethodDto>> GetCheckoutMethods(
        [FromServices] Biogarden.BusinessLogic.Services.CheckoutPricingService pricing) => Ok(pricing.GetMethods());

    [HttpPost("quote")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.CartMutation)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<CheckoutQuoteDto>> GetCheckoutQuote(
        [FromBody] CheckoutQuoteRequest request, CancellationToken cancellationToken)
        => Ok(await _orderService.GetCheckoutQuoteAsync(GetCurrentUserIdOrNull(), GetCartSessionId(), request, cancellationToken));

    [HttpPost("{orderId:guid}/sumup-checkout")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Payment)]
    [ProducesResponseType(typeof(SumUpCheckoutDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SumUpCheckoutDto>> CreateSumUpCheckout(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var checkout = await _orderService.CreateSumUpCheckoutAsync(
            GetCurrentUserIdOrNull(),
            GetCartSessionId(),
            orderId,
            cancellationToken);

        return Ok(checkout);
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

    [HttpPost("{orderId:guid}/withdrawal-requests")]
    [ProducesResponseType(typeof(OrderWithdrawalRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderWithdrawalRequestDto>> RequestWithdrawal(
        Guid orderId,
        [FromBody] CreateOrderWithdrawalRequestDto requestDto,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var request = await _orderService.RequestWithdrawalAsync(
            userId,
            orderId,
            requestDto,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, request);
    }

    [HttpPost("withdrawal-requests")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.PublicForm)]
    [ProducesResponseType(typeof(OrderWithdrawalRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderWithdrawalRequestDto>> RequestWithdrawalByOrderLookup(
        [FromBody] PublicOrderWithdrawalRequestDto requestDto,
        CancellationToken cancellationToken)
    {
        var request = await _orderService.RequestWithdrawalByOrderLookupAsync(
            requestDto,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, request);
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

    [HttpGet("{orderId:guid}/payment-status")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicyNames.Payment)]
    [ProducesResponseType(typeof(PublicOrderPaymentStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicOrderPaymentStatusDto>> GetPublicOrderPaymentStatus(
        Guid orderId,
        [FromQuery] string sumUpCheckoutReference,
        CancellationToken cancellationToken)
    {
        var status = await _orderService.GetPublicOrderPaymentStatusAsync(
            orderId,
            sumUpCheckoutReference,
            cancellationToken);

        return Ok(status);
    }

    private Guid? GetCurrentUserIdOrNull()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return userId;
    }

    private Guid GetCurrentUserId()
    {
        return GetCurrentUserIdOrNull()
            ?? throw new UnauthorizedAccessException("Authenticated user id is missing or invalid.");
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
