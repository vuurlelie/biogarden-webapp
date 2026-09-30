using Biogarden.BusinessLogic.DTOs.Checkout;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class SaveCheckoutDraftValidator : AbstractValidator<SaveCheckoutDraftDto>
{
    public SaveCheckoutDraftValidator()
    {
        RuleFor(x => x.Fields).NotNull();
        When(x => x.Fields is not null, () =>
        {
            RuleFor(x => x.Fields.FulfillmentMethod).IsInEnum().When(x => x.Fields.FulfillmentMethod.HasValue);
            RuleFor(x => x.Fields.GlsDeliveryPointId).MaximumLength(100);
            RuleFor(x => x.Fields.CustomerName).NotNull().MaximumLength(200);
            RuleFor(x => x.Fields.CustomerEmail).NotNull().MaximumLength(256);
            RuleFor(x => x.Fields.CustomerPhone).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.PostalCode).NotNull().MaximumLength(20);
            RuleFor(x => x.Fields.City).NotNull().MaximumLength(100);
            RuleFor(x => x.Fields.Street).NotNull().MaximumLength(200);
            RuleFor(x => x.Fields.HouseNumber).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.Building).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.Floor).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.Door).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.Doorbell).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.BillingCompanyName).NotNull().MaximumLength(200);
            RuleFor(x => x.Fields.BillingTaxNumber).NotNull().MaximumLength(20);
            RuleFor(x => x.Fields.DeliveryPostalCode).NotNull().MaximumLength(20);
            RuleFor(x => x.Fields.DeliveryCity).NotNull().MaximumLength(100);
            RuleFor(x => x.Fields.DeliveryStreet).NotNull().MaximumLength(200);
            RuleFor(x => x.Fields.DeliveryHouseNumber).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.DeliveryBuilding).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.DeliveryFloor).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.DeliveryDoor).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.DeliveryDoorbell).NotNull().MaximumLength(50);
            RuleFor(x => x.Fields.CustomerType).InclusiveBetween(0, 1);
        });
    }
}
