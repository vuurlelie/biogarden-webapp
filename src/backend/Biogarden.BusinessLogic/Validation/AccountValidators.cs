using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.BusinessLogic.DTOs.Addresses;
using Biogarden.BusinessLogic.DTOs.Users;
using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.BusinessLogic.DTOs.Carts;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.DTOs.Admin;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class AddressCreateDtoValidator : AbstractValidator<AddressCreateDto>
{
    public AddressCreateDtoValidator()
    {
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
        When(x => x.CustomerType == CustomerType.Company, () =>
        {
            RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200).WithMessage("A cégnév kötelező, legfeljebb 200 karakterrel.");
            RuleFor(x => x.TaxNumber).Must(ValidationRules.ValidTaxNumber).WithMessage("Hibás adószám. Példa: 12345678-1-12.");
        });
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
