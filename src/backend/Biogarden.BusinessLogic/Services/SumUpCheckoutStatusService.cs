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
        SumUpCheckoutStatusChangedEvent? statusEvent;
        try { statusEvent = JsonSerializer.Deserialize<SumUpCheckoutStatusChangedEvent>(payload); }
        catch (JsonException) { throw new ArgumentException("Invalid payment notification JSON."); }
        if (statusEvent is null || string.IsNullOrWhiteSpace(statusEvent.EventType))
            throw new ArgumentException("Payment event type is required.");
        if (!string.Equals(statusEvent.EventType, CheckoutStatusChanged, StringComparison.Ordinal)) return;
        if (string.IsNullOrWhiteSpace(statusEvent.Id) || statusEvent.Id.Length > 100 ||
            statusEvent.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("Invalid payment checkout ID.");

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
        if (normalizedStatus is not (Paid or Failed or Expired or "PENDING"))
            throw new InvalidOperationException("Unknown SumUp checkout status.");
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
            await _orderService.ReconcileSumUpCheckoutAsync(checkout, cancellationToken);

            paymentEvent.ProcessedAtUtc = DateTime.UtcNow;
            paymentEvent.ProcessingFailedAtUtc = null;
            paymentEvent.ProcessingError = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // A failed save can leave both the order and event dirty. Persist diagnostics only.
            _unitOfWork.ClearTrackedChanges();
            var failedEvent = await _unitOfWork.PaymentProviderEventRepository.GetByProviderAndEventKeyAsync(Provider, eventKey, cancellationToken);
            if (failedEvent is not null && !failedEvent.ProcessedAtUtc.HasValue)
            {
                failedEvent.LastProcessingAttemptAtUtc = attemptedAtUtc;
                failedEvent.ProcessingFailedAtUtc = DateTime.UtcNow;
                failedEvent.ProcessingError = Truncate(exception.Message, ProcessingErrorMaxLength);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
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
            // Remove the losing Added entity before loading the winner of a unique-key race.
            _unitOfWork.ClearTrackedChanges();
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
