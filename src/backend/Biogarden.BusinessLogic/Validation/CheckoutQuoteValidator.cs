using Biogarden.BusinessLogic.DTOs.Orders;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class CheckoutQuoteValidator : AbstractValidator<CheckoutQuoteRequest>
{
    public CheckoutQuoteValidator()
    {
        RuleFor(checkoutQuoteRequest => checkoutQuoteRequest.FulfillmentMethod).IsInEnum().WithMessage("Érvénytelen átvételi mód.");
        RuleFor(checkoutQuoteRequest => checkoutQuoteRequest.PaymentMethod).IsInEnum().WithMessage("Érvénytelen fizetési mód.");
        RuleFor(checkoutQuoteRequest => checkoutQuoteRequest.GlsDeliveryPointId).MaximumLength(100);
        RuleFor(checkoutQuoteRequest => checkoutQuoteRequest.GlsDeliveryPointId).NotEmpty().When(checkoutQuoteRequest => checkoutQuoteRequest.FulfillmentMethod is
            Biogarden.DataAccess.Enums.FulfillmentMethod.ParcelLocker or Biogarden.DataAccess.Enums.FulfillmentMethod.ParcelShop);
    }
}
