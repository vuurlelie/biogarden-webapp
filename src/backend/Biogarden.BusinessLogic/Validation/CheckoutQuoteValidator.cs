using Biogarden.BusinessLogic.DTOs.Orders;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class CheckoutQuoteValidator : AbstractValidator<CheckoutQuoteRequest>
{
    public CheckoutQuoteValidator()
    {
        RuleFor(x => x.FulfillmentMethod).IsInEnum().WithMessage("Érvénytelen átvételi mód.");
        RuleFor(x => x.PaymentMethod).IsInEnum().WithMessage("Érvénytelen fizetési mód.");
        RuleFor(x => x.GlsDeliveryPointId).MaximumLength(100);
        RuleFor(x => x.GlsDeliveryPointId).NotEmpty().When(x => x.FulfillmentMethod is
            Biogarden.DataAccess.Enums.FulfillmentMethod.ParcelLocker or Biogarden.DataAccess.Enums.FulfillmentMethod.ParcelShop);
    }
}
