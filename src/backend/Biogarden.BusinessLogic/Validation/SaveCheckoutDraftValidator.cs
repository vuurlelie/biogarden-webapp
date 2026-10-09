using Biogarden.BusinessLogic.DTOs.Checkout;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class SaveCheckoutDraftValidator : AbstractValidator<SaveCheckoutDraftDto>
{
    public SaveCheckoutDraftValidator()
    {
        RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields).NotNull();
        When(saveCheckoutDraft => saveCheckoutDraft.Fields is not null, () =>
        {
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.FulfillmentMethod).IsInEnum().When(saveCheckoutDraft => saveCheckoutDraft.Fields.FulfillmentMethod.HasValue);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.GlsDeliveryPointId).MaximumLength(100);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.CustomerName).NotNull().MaximumLength(200);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.CustomerEmail).NotNull().MaximumLength(256);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.CustomerPhone).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.PostalCode).NotNull().MaximumLength(20);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.City).NotNull().MaximumLength(100);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.Street).NotNull().MaximumLength(200);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.HouseNumber).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.Building).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.Floor).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.Door).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.Doorbell).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.BillingCompanyName).NotNull().MaximumLength(200);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.BillingTaxNumber).NotNull().MaximumLength(20);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryPostalCode).NotNull().MaximumLength(20);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryCity).NotNull().MaximumLength(100);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryStreet).NotNull().MaximumLength(200);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryHouseNumber).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryBuilding).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryFloor).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryDoor).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.DeliveryDoorbell).NotNull().MaximumLength(50);
            RuleFor(saveCheckoutDraft => saveCheckoutDraft.Fields.CustomerType).InclusiveBetween(0, 1);
        });
    }
}
