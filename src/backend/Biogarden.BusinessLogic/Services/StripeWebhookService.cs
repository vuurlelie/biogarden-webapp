using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Options;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Biogarden.BusinessLogic.Services;

public sealed class StripeWebhookService : IStripeWebhookService
{
    private readonly StripeOptions _options;
    private readonly IOrderService _orderService;

    public StripeWebhookService(
        IOptions<StripeOptions> options,
        IOrderService orderService)
    {
        _options = options.Value;
        _orderService = orderService;
    }

    public async Task HandleAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            throw new InvalidOperationException("Stripe webhook secret is not configured.");
        }

        var stripeEvent = EventUtility.ConstructEvent(
            payload,
            signatureHeader,
            _options.WebhookSecret);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.async_payment_succeeded":
                await HandleCheckoutSessionSucceededAsync(stripeEvent, cancellationToken);
                break;

            case "checkout.session.expired":
            case "checkout.session.async_payment_failed":
                await HandleCheckoutSessionFailedAsync(stripeEvent, cancellationToken);
                break;
        }
    }

    private async Task HandleCheckoutSessionSucceededAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Session session)
        {
            return;
        }

        await _orderService.ConfirmStripeCheckoutSessionAsync(
            session.Id,
            session.PaymentIntentId,
            cancellationToken);
    }

    private async Task HandleCheckoutSessionFailedAsync(
        Event stripeEvent,
        CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Session session)
        {
            return;
        }

        await _orderService.MarkStripePaymentFailedAsync(
            session.Id,
            session.PaymentIntentId,
            cancellationToken);
    }
}
