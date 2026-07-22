using System.Text.Json;
using System.Text.Json.Serialization;
using Biogarden.BusinessLogic.Abstractions;

namespace Biogarden.BusinessLogic.Services;

public sealed class SumUpCheckoutStatusService : ISumUpCheckoutStatusService
{
    private const string CheckoutStatusChanged = "CHECKOUT_STATUS_CHANGED";
    private const string Paid = "PAID";
    private const string Failed = "FAILED";
    private const string Expired = "EXPIRED";

    private readonly ISumUpCheckoutService _checkoutService;
    private readonly IOrderService _orderService;

    public SumUpCheckoutStatusService(
        ISumUpCheckoutService checkoutService,
        IOrderService orderService)
    {
        _checkoutService = checkoutService;
        _orderService = orderService;
    }

    public async Task HandleAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        var statusEvent = JsonSerializer.Deserialize<SumUpCheckoutStatusChangedEvent>(payload);

        if (statusEvent is null ||
            !string.Equals(statusEvent.EventType, CheckoutStatusChanged, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(statusEvent.Id))
        {
            return;
        }

        var checkout = await _checkoutService.GetCheckoutAsync(
            statusEvent.Id,
            cancellationToken);

        switch (checkout.Status.ToUpperInvariant())
        {
            case Paid:
                await _orderService.ConfirmSumUpCheckoutAsync(
                    checkout.CheckoutId,
                    checkout.TransactionCode,
                    cancellationToken);
                break;

            case Failed:
            case Expired:
                await _orderService.MarkSumUpPaymentFailedAsync(
                    checkout.CheckoutId,
                    checkout.TransactionCode,
                    cancellationToken);
                break;
        }
    }

    private sealed class SumUpCheckoutStatusChangedEvent
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; init; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
    }
}
