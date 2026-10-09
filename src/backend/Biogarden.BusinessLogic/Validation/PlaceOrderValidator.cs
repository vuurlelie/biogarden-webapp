using Biogarden.BusinessLogic.Constants;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderDto>
{
    public PlaceOrderValidator()
    {
        // Required for a new order in the service; optional on an idempotent retry.
        RuleFor(orderRequest => orderRequest.PaymentMethod).IsInEnum().When(orderRequest => orderRequest.PaymentMethod.HasValue);
        RuleFor(orderRequest => orderRequest.QuoteVersion).Matches("^[A-F0-9]{64}$").When(orderRequest => orderRequest.QuoteVersion is not null)
            .WithMessage("Az árajánlat azonosítója érvénytelen. Tekintsd át újra a rendelést.");
        RuleFor(orderRequest => orderRequest.IdempotencyKey).Must(value => value is not null &&
            System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^[A-Za-z0-9_-]{8,100}$"))
            .WithMessage("A rendelés azonosító kulcsa érvénytelen. Frissítsd az oldalt.");
        RuleFor(orderRequest => orderRequest.FulfillmentMethod).IsInEnum().WithMessage("Érvénytelen átvételi mód.");
        RuleFor(orderRequest => orderRequest.CustomerType).IsInEnum().WithMessage("Érvénytelen vásárlói típus.");
        RuleFor(orderRequest => orderRequest.GlsDeliveryPointId).MaximumLength(100);
        RuleFor(orderRequest => orderRequest.GlsDeliveryPointId).NotEmpty().When(orderRequest => orderRequest.FulfillmentMethod is FulfillmentMethod.ParcelLocker or FulfillmentMethod.ParcelShop);
        RuleFor(orderRequest => orderRequest.AddressId).GreaterThan(0).When(orderRequest => orderRequest.AddressId.HasValue).WithMessage("Érvénytelen számlázási cím.");
        RuleFor(orderRequest => orderRequest.DeliveryAddressId).GreaterThan(0).When(orderRequest => orderRequest.DeliveryAddressId.HasValue).WithMessage("Érvénytelen szállítási cím.");
        RuleFor(orderRequest => orderRequest.AcceptedTermsVersion).Equal(LegalDocumentVersions.CurrentTermsVersion)
            .WithMessage("Az aktuális Általános Szerződési Feltételek elfogadása kötelező.");
        RuleFor(orderRequest => orderRequest.AcceptedPrivacyPolicyVersion).Equal(LegalDocumentVersions.CurrentPrivacyPolicyVersion)
            .WithMessage("Az aktuális Adatkezelési Tájékoztató elfogadása kötelező.");
        RuleFor(orderRequest => orderRequest.CustomerName).MaximumLength(200).WithMessage("A név legfeljebb 200 karakter lehet.");
        RuleFor(orderRequest => orderRequest.CustomerEmail).MaximumLength(256).WithMessage("Az email cím legfeljebb 256 karakter lehet.");
        RuleFor(orderRequest => orderRequest.CustomerPhone).MaximumLength(50).WithMessage("A telefonszám legfeljebb 50 karakter lehet.");
        RuleFor(orderRequest => orderRequest.BillingCompanyName).MaximumLength(200).WithMessage("A cégnév legfeljebb 200 karakter lehet.");
        RuleFor(orderRequest => orderRequest.BillingTaxNumber).MaximumLength(20).WithMessage("Az adószám legfeljebb 20 karakter lehet.");

        RuleFor(orderRequest => orderRequest.PostalCode).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.City).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.Street).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.HouseNumber).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.Building).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.Floor).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.Door).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.Doorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryPostalCode).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryCity).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryStreet).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryHouseNumber).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryBuilding).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryFloor).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryDoor).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(orderRequest => orderRequest.DeliveryDoorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");

        RuleFor(orderRequest => orderRequest).Custom((orderRequest, context) =>
        {
            var authenticated = context.RootContextData.TryGetValue("Authenticated", out var value) && value is true;
            var manualContact = !string.IsNullOrWhiteSpace(orderRequest.CustomerName) ||
                !string.IsNullOrWhiteSpace(orderRequest.CustomerEmail) || !string.IsNullOrWhiteSpace(orderRequest.CustomerPhone);
            if (!authenticated || manualContact)
            {
                Required(context, nameof(orderRequest.CustomerName), orderRequest.CustomerName);
                Required(context, nameof(orderRequest.CustomerPhone), orderRequest.CustomerPhone);
                if (!System.Net.Mail.MailAddress.TryCreate(orderRequest.CustomerEmail?.Trim(), out _))
                    context.AddFailure(nameof(orderRequest.CustomerEmail), "Adj meg egy érvényes email címet.");
            }
            // Authenticated customer type and saved billing data are resolved and checked in the service.
            if (!authenticated && orderRequest.CustomerType == CustomerType.Company)
            {
                if (!orderRequest.AddressId.HasValue)
                    Address(context, orderRequest.PostalCode, orderRequest.City, orderRequest.Street, orderRequest.HouseNumber, "");
                var company = !string.IsNullOrWhiteSpace(orderRequest.BillingCompanyName);
                var tax = !string.IsNullOrWhiteSpace(orderRequest.BillingTaxNumber);
                if (company != tax)
                {
                    context.AddFailure(nameof(orderRequest.BillingCompanyName), "A cégnév és az adószám együtt szükséges.");
                    context.AddFailure(nameof(orderRequest.BillingTaxNumber), "A cégnév és az adószám együtt szükséges.");
                }
                if (tax && !ValidationRules.ValidTaxNumber(orderRequest.BillingTaxNumber))
                    context.AddFailure(nameof(orderRequest.BillingTaxNumber), "Hibás adószám. Példa: 12345678-1-12.");
            }
            if (orderRequest.FulfillmentMethod == FulfillmentMethod.HomeDelivery && !orderRequest.DeliveryAddressId.HasValue)
                Address(context, orderRequest.DeliveryPostalCode, orderRequest.DeliveryCity, orderRequest.DeliveryStreet, orderRequest.DeliveryHouseNumber, "Delivery");
        });
    }

    private static void Required(ValidationContext<PlaceOrderDto> context, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            context.AddFailure(name, "A mező megadása kötelező.");
    }

    private static void Address(ValidationContext<PlaceOrderDto> context, string? postalCode, string? city, string? street, string? houseNumber, string prefix)
    {
        Required(context, prefix + "PostalCode", postalCode);
        Required(context, prefix + "City", city);
        Required(context, prefix + "Street", street);
        Required(context, prefix + "HouseNumber", houseNumber);
    }
}
