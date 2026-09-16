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

public sealed class AddCartItemDtoValidator : AbstractValidator<AddCartItemDto>
{
    public AddCartItemDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("Érvénytelen termékazonosító.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("A mennyiség legyen pozitív.");
    }
}

public sealed class UpdateCartItemQuantityDtoValidator : AbstractValidator<UpdateCartItemQuantityDto>
{
    public UpdateCartItemQuantityDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("A mennyiség legyen pozitív.");
    }
}

public sealed class ContactMessageRequestDtoValidator : AbstractValidator<ContactMessageRequestDto>
{
    public ContactMessageRequestDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Name).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(x => x.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(x => x.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(x => x.Phone).MaximumLength(40).WithMessage("Legfeljebb 40 karakter adható meg.");
        RuleFor(x => x.Subject).MaximumLength(150).WithMessage("Legfeljebb 150 karakter adható meg.");
        RuleFor(x => x.Message).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Message).Length(10, 2000).WithMessage("Az üzenet 10–2000 karakter legyen.");
    }
}

public sealed class CancelOrderDtoValidator : AbstractValidator<CancelOrderDto>
{
    public CancelOrderDtoValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
    }
}

public sealed class CreateOrderWithdrawalRequestDtoValidator : AbstractValidator<CreateOrderWithdrawalRequestDto>
{
    public CreateOrderWithdrawalRequestDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Reason).Must(value => value is not null && value.Trim().Length >= 10 && value.Trim().Length <= 1000).WithMessage("Az indoklás 10–1000 karakter legyen.");
    }
}

public sealed class PublicOrderWithdrawalRequestDtoValidator : AbstractValidator<PublicOrderWithdrawalRequestDto>
{
    public PublicOrderWithdrawalRequestDtoValidator()
    {
        RuleFor(x => x.OrderNumber).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.OrderNumber).MaximumLength(32).WithMessage("Legfeljebb 32 karakter adható meg.");
        RuleFor(x => x.CustomerEmail).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(x => x.CustomerEmail).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(x => x.Reason).Must(value => value is not null && value.Trim().Length >= 10 && value.Trim().Length <= 1000).WithMessage("Az indoklás 10–1000 karakter legyen.");
    }
}

public sealed class AdminOrderUpdateDtoValidator : AbstractValidator<AdminOrderUpdateDto>
{
    public AdminOrderUpdateDtoValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.PaymentStatus).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.CancellationReason).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
    }
}

public sealed class UpdateAdminInventoryDtoValidator : AbstractValidator<UpdateAdminInventoryDto>
{
    public UpdateAdminInventoryDtoValidator()
    {
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("A készlet nem lehet negatív.");
    }
}

public sealed class AdminAccountLifecycleDecisionDtoValidator : AbstractValidator<AdminAccountLifecycleDecisionDto>
{
    public AdminAccountLifecycleDecisionDtoValidator()
    {
        RuleFor(x => x.AdminNote).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
    }
}
