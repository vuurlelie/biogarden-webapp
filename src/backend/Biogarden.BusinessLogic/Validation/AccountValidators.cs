using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class AddressCreateDtoValidator : AbstractValidator<AddressCreateDto>
{
    public AddressCreateDtoValidator()
    {
        RuleFor(x => x.PostalCode).MaximumLength(4).WithMessage("Legfeljebb 4 karakter adható meg.");
        RuleFor(x => x.City).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(x => x.Street).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(x => x.HouseNumber).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Building).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Floor).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Door).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Doorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.PostalCode).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.City).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Street).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.HouseNumber).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class AddressEditDtoValidator : AbstractValidator<AddressEditDto>
{
    public AddressEditDtoValidator()
    {
        RuleFor(x => x.PostalCode).MaximumLength(4).WithMessage("Legfeljebb 4 karakter adható meg.");
        RuleFor(x => x.City).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(x => x.Street).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(x => x.HouseNumber).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Building).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Floor).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Door).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(x => x.Doorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(x => x.PostalCode).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.City).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Street).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.HouseNumber).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class UpdateUserAddressesDtoValidator : AbstractValidator<UpdateUserAddressesDto>
{
    public UpdateUserAddressesDtoValidator()
    {
        RuleFor(x => x.ShippingAddress!).SetValidator(new AddressCreateDtoValidator()).When(x => x.ShippingAddress is not null);
        RuleFor(x => x.BillingAddress!).SetValidator(new AddressCreateDtoValidator()).When(x => !x.UseShippingAsBilling && x.BillingAddress is not null);
    }
}

public sealed class UpdateUserProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
{
    public UpdateUserProfileDtoValidator()
    {
        RuleFor(x => x.CustomerType).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.FirstName).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(x => x.LastName).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        // Profile editing permits incomplete company details; registration and checkout have different requirements.
        RuleFor(x => x.CompanyName).MaximumLength(200).WithMessage("A cégnév legfeljebb 200 karakter lehet.");
        RuleFor(x => x.TaxNumber).Must(ValidationRules.ValidTaxNumber)
            .When(x => !string.IsNullOrWhiteSpace(x.TaxNumber)).WithMessage("Hibás adószám. Példa: 12345678-1-12.");
    }
}

public sealed class AccountLifecycleRequestDtoValidator : AbstractValidator<AccountLifecycleRequestDto>
{
    public AccountLifecycleRequestDtoValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.Reason).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
    }
}
