using System.Text.Json;
using System.Text.Json.Serialization;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.DataAccess.Abstractions;
using Biogarden.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Biogarden.BusinessLogic.Services;

public sealed class SumUpCheckoutStatusService : ISumUpCheckoutStatusService
{
    private const string Provider = "SumUp";
    private const string CheckoutStatusChanged = "CHECKOUT_STATUS_CHANGED";
    private const string Paid = "PAID";
    private const string Failed = "FAILED";
    private const string Expired = "EXPIRED";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISumUpCheckoutService _checkoutService;
    private readonly IOrderService _orderService;

    public SumUpCheckoutStatusService(
        IUnitOfWork unitOfWork,
        ISumUpCheckoutService checkoutService,
        IOrderService orderService)
    {
        _unitOfWork = unitOfWork;
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

        var eventKey = $"{statusEvent.EventType}:{statusEvent.Id.Trim()}";
        var paymentEvent = await GetOrCreatePaymentEventAsync(
            eventKey,
            statusEvent,
            cancellationToken);
        if (paymentEvent.ProcessedAtUtc.HasValue)
        {
            return;
        }

        var checkout = await _checkoutService.GetCheckoutAsync(
            statusEvent.Id,
            cancellationToken);

        if (!string.Equals(checkout.CheckoutId, statusEvent.Id.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SumUp checkout identity mismatch.");
        }

        paymentEvent.Status = checkout.Status;

        switch (checkout.Status.ToUpperInvariant())
        {
            case Paid:
                await _orderService.ConfirmSumUpCheckoutAsync(
                    checkout.CheckoutId,
                    checkout.TransactionCode,
                    checkout.Amount,
                    checkout.Currency,
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

        paymentEvent.ProcessedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<PaymentProviderEvent> GetOrCreatePaymentEventAsync(
        string eventKey,
        SumUpCheckoutStatusChangedEvent statusEvent,
        CancellationToken cancellationToken)
    {
        var existingEvent = await _unitOfWork.PaymentProviderEventRepository.GetByProviderAndEventKeyAsync(
            Provider,
            eventKey,
            cancellationToken);
        if (existingEvent is not null)
        {
            return existingEvent;
        }

        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var receivedEvent = new PaymentProviderEvent
                {
                    Id = Guid.NewGuid(),
                    Provider = Provider,
                    EventKey = eventKey,
                    CheckoutId = statusEvent.Id.Trim(),
                    EventType = statusEvent.EventType,
                    ReceivedAtUtc = DateTime.UtcNow
                };

                await _unitOfWork.PaymentProviderEventRepository.AddAsync(receivedEvent, token);
                await _unitOfWork.SaveChangesAsync(token);

                return receivedEvent;
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrentEvent = await _unitOfWork.PaymentProviderEventRepository.GetByProviderAndEventKeyAsync(
                Provider,
                eventKey,
                cancellationToken);
            if (concurrentEvent is not null)
            {
                return concurrentEvent;
            }

            throw;
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
