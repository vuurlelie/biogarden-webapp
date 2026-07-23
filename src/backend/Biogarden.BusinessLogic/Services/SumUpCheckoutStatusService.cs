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
    private const int ProcessingErrorMaxLength = 1000;

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

        var checkoutId = statusEvent.Id.Trim();
        var eventType = statusEvent.EventType.Trim();
        var checkout = await _checkoutService.GetCheckoutAsync(
            checkoutId,
            cancellationToken);

        if (!string.Equals(checkout.CheckoutId, checkoutId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SumUp checkout identity mismatch.");
        }

        var normalizedStatus = checkout.Status.Trim().ToUpperInvariant();
        var eventKey = $"{eventType}:{checkoutId}:{normalizedStatus}";
        var paymentEvent = await GetOrCreatePaymentEventAsync(
            eventKey,
            statusEvent,
            normalizedStatus,
            cancellationToken);
        if (paymentEvent.ProcessedAtUtc.HasValue)
        {
            return;
        }

        var attemptedAtUtc = DateTime.UtcNow;
        paymentEvent.LastProcessingAttemptAtUtc = attemptedAtUtc;
        paymentEvent.Status = normalizedStatus;

        try
        {
            switch (normalizedStatus)
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
            paymentEvent.ProcessingFailedAtUtc = null;
            paymentEvent.ProcessingError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            paymentEvent.ProcessingFailedAtUtc = DateTime.UtcNow;
            paymentEvent.ProcessingError = Truncate(exception.Message, ProcessingErrorMaxLength);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private async Task<PaymentProviderEvent> GetOrCreatePaymentEventAsync(
        string eventKey,
        SumUpCheckoutStatusChangedEvent statusEvent,
        string status,
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
                    Status = status,
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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private sealed class SumUpCheckoutStatusChangedEvent
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; init; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;
    }
}
