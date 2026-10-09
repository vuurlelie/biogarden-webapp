using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class AddressCreateDtoValidator : AbstractValidator<AddressCreateDto>
{
    public AddressCreateDtoValidator()
    {
        RuleFor(addressCreate => addressCreate.PostalCode).MaximumLength(4).WithMessage("Legfeljebb 4 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.City).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.Street).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.HouseNumber).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.Building).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.Floor).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.Door).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.Doorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(addressCreate => addressCreate.PostalCode).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(addressCreate => addressCreate.City).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(addressCreate => addressCreate.Street).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(addressCreate => addressCreate.HouseNumber).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class AddressEditDtoValidator : AbstractValidator<AddressEditDto>
{
    public AddressEditDtoValidator()
    {
        RuleFor(addressEdit => addressEdit.PostalCode).MaximumLength(4).WithMessage("Legfeljebb 4 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.City).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.Street).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.HouseNumber).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.Building).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.Floor).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.Door).MaximumLength(20).WithMessage("Legfeljebb 20 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.Doorbell).MaximumLength(50).WithMessage("Legfeljebb 50 karakter adható meg.");
        RuleFor(addressEdit => addressEdit.PostalCode).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(addressEdit => addressEdit.City).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(addressEdit => addressEdit.Street).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(addressEdit => addressEdit.HouseNumber).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class UpdateUserAddressesDtoValidator : AbstractValidator<UpdateUserAddressesDto>
{
    public UpdateUserAddressesDtoValidator()
    {
        RuleFor(updateUserAddresses => updateUserAddresses.ShippingAddress!).SetValidator(new AddressCreateDtoValidator()).When(updateUserAddresses => updateUserAddresses.ShippingAddress is not null);
        RuleFor(updateUserAddresses => updateUserAddresses.BillingAddress!).SetValidator(new AddressCreateDtoValidator()).When(updateUserAddresses => !updateUserAddresses.UseShippingAsBilling && updateUserAddresses.BillingAddress is not null);
    }
}

public sealed class UpdateUserProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
{
    public UpdateUserProfileDtoValidator()
    {
        RuleFor(updateUserProfile => updateUserProfile.CustomerType).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(updateUserProfile => updateUserProfile.FirstName).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(updateUserProfile => updateUserProfile.LastName).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        // Profile editing permits incomplete company details; registration and checkout have different requirements.
        RuleFor(updateUserProfile => updateUserProfile.CompanyName).MaximumLength(200).WithMessage("A cégnév legfeljebb 200 karakter lehet.");
        RuleFor(updateUserProfile => updateUserProfile.TaxNumber).Must(ValidationRules.ValidTaxNumber)
            .When(updateUserProfile => !string.IsNullOrWhiteSpace(updateUserProfile.TaxNumber)).WithMessage("Hibás adószám. Példa: 12345678-1-12.");
    }
}

public sealed class AccountLifecycleRequestDtoValidator : AbstractValidator<AccountLifecycleRequestDto>
{
    public AccountLifecycleRequestDtoValidator()
    {
        RuleFor(accountLifecycleRequest => accountLifecycleRequest.Type).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(accountLifecycleRequest => accountLifecycleRequest.Reason).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
    }
}
