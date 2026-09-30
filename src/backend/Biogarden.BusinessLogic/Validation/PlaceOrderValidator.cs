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
        RuleFor(x => x.PaymentMethod).IsInEnum().When(x => x.PaymentMethod.HasValue);
        RuleFor(x => x.QuoteVersion).Matches("^[A-F0-9]{64}$").When(x => x.QuoteVersion is not null)
            .WithMessage("Az árajánlat azonosítója érvénytelen. Tekintsd át újra a rendelést.");
        RuleFor(x => x.IdempotencyKey).Must(value => value is not null &&
            System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^[A-Za-z0-9_-]{8,100}$"))
            .WithMessage("A rendelés azonosító kulcsa érvénytelen. Frissítsd az oldalt.");
        RuleFor(x => x.FulfillmentMethod).IsInEnum().WithMessage("Érvénytelen átvételi mód.");
        RuleFor(x => x.CustomerType).IsInEnum().WithMessage("Érvénytelen vásárlói típus.");
        RuleFor(x => x.AddressId).GreaterThan(0).When(x => x.AddressId.HasValue).WithMessage("Érvénytelen számlázási cím.");
        RuleFor(x => x.DeliveryAddressId).GreaterThan(0).When(x => x.DeliveryAddressId.HasValue).WithMessage("Érvénytelen szállítási cím.");
        RuleFor(x => x.AcceptedTermsVersion).Equal(LegalDocumentVersions.CurrentTermsVersion)
            .WithMessage("Az aktuális Általános Szerződési Feltételek elfogadása kötelező.");
        RuleFor(x => x.AcceptedPrivacyPolicyVersion).Equal(LegalDocumentVersions.CurrentPrivacyPolicyVersion)
            .WithMessage("Az aktuális Adatkezelési Tájékoztató elfogadása kötelező.");
        RuleFor(x => x.CustomerName).MaximumLength(200).WithMessage("A név legfeljebb 200 karakter lehet.");
        RuleFor(x => x.CustomerEmail).MaximumLength(256).WithMessage("Az email cím legfeljebb 256 karakter lehet.");
        RuleFor(x => x.CustomerPhone).MaximumLength(50).WithMessage("A telefonszám legfeljebb 50 karakter lehet.");
        RuleFor(x => x.BillingCompanyName).MaximumLength(200).WithMessage("A cégnév legfeljebb 200 karakter lehet.");
        RuleFor(x => x.BillingTaxNumber).MaximumLength(20).WithMessage("Az adószám legfeljebb 20 karakter lehet.");

        RuleFor(x => x.PostalCode).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.City).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(x => x.Street).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(x => x.HouseNumber).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.Building).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.Floor).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.Door).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.Doorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.DeliveryPostalCode).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.DeliveryCity).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(x => x.DeliveryStreet).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(x => x.DeliveryHouseNumber).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.DeliveryBuilding).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.DeliveryFloor).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.DeliveryDoor).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.DeliveryDoorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");

        RuleFor(x => x).Custom((dto, context) =>
        {
            var authenticated = context.RootContextData.TryGetValue("Authenticated", out var value) && value is true;
            var manualContact = !string.IsNullOrWhiteSpace(dto.CustomerName) ||
                !string.IsNullOrWhiteSpace(dto.CustomerEmail) || !string.IsNullOrWhiteSpace(dto.CustomerPhone);
            if (!authenticated || manualContact)
            {
                Required(context, nameof(dto.CustomerName), dto.CustomerName);
                Required(context, nameof(dto.CustomerPhone), dto.CustomerPhone);
                if (!System.Net.Mail.MailAddress.TryCreate(dto.CustomerEmail?.Trim(), out _))
                    context.AddFailure(nameof(dto.CustomerEmail), "Adj meg egy érvényes email címet.");
            }
            // Authenticated customer type and saved billing data are resolved and checked in the service.
            if (!authenticated && dto.CustomerType == CustomerType.Company)
            {
                if (!dto.AddressId.HasValue)
                    Address(context, dto.PostalCode, dto.City, dto.Street, dto.HouseNumber, "");
                var company = !string.IsNullOrWhiteSpace(dto.BillingCompanyName);
                var tax = !string.IsNullOrWhiteSpace(dto.BillingTaxNumber);
                if (company != tax)
                {
                    context.AddFailure(nameof(dto.BillingCompanyName), "A cégnév és az adószám együtt szükséges.");
                    context.AddFailure(nameof(dto.BillingTaxNumber), "A cégnév és az adószám együtt szükséges.");
                }
                if (tax && !ValidationRules.ValidTaxNumber(dto.BillingTaxNumber))
                    context.AddFailure(nameof(dto.BillingTaxNumber), "Hibás adószám. Példa: 12345678-1-12.");
            }
            if (dto.FulfillmentMethod != FulfillmentMethod.PersonalPickup && !dto.DeliveryAddressId.HasValue)
                Address(context, dto.DeliveryPostalCode, dto.DeliveryCity, dto.DeliveryStreet, dto.DeliveryHouseNumber, "Delivery");
        });
    }

    private static void Required(ValidationContext<PlaceOrderDto> context, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) context.AddFailure(name, "A mező megadása kötelező.");
    }

    private static void Address(ValidationContext<PlaceOrderDto> context, string? postalCode,
        string? city, string? street, string? houseNumber, string prefix)
    {
        Required(context, prefix + "PostalCode", postalCode);
        Required(context, prefix + "City", city);
        Required(context, prefix + "Street", street);
        Required(context, prefix + "HouseNumber", houseNumber);
    }
}
