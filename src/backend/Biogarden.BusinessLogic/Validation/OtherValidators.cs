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
        RuleFor(addCartItem => addCartItem.ProductId).GreaterThan(0).WithMessage("Érvénytelen termékazonosító.");
        RuleFor(addCartItem => addCartItem.Quantity).GreaterThan(0).WithMessage("A mennyiség legyen pozitív.");
    }
}

public sealed class UpdateCartItemQuantityDtoValidator : AbstractValidator<UpdateCartItemQuantityDto>
{
    public UpdateCartItemQuantityDtoValidator()
    {
        RuleFor(updateCartItemQuantity => updateCartItemQuantity.Quantity).GreaterThan(0).WithMessage("A mennyiség legyen pozitív.");
    }
}

public sealed class ContactMessageRequestDtoValidator : AbstractValidator<ContactMessageRequestDto>
{
    public ContactMessageRequestDtoValidator()
    {
        RuleFor(contactMessageRequest => contactMessageRequest.SubmissionId).NotEmpty().WithMessage("Az üzenetazonosító megadása kötelező.");
        RuleFor(contactMessageRequest => contactMessageRequest.Name).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(contactMessageRequest => contactMessageRequest.Name).MaximumLength(100).WithMessage("Legfeljebb 100 karakter adható meg.");
        RuleFor(contactMessageRequest => contactMessageRequest.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(contactMessageRequest => contactMessageRequest.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(contactMessageRequest => contactMessageRequest.Phone).MaximumLength(40).WithMessage("Legfeljebb 40 karakter adható meg.");
        RuleFor(contactMessageRequest => contactMessageRequest.Subject).MaximumLength(150).WithMessage("Legfeljebb 150 karakter adható meg.");
        RuleFor(contactMessageRequest => contactMessageRequest.Message).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(contactMessageRequest => contactMessageRequest.Message).Length(10, 2000).WithMessage("Az üzenet 10–2000 karakter legyen.");
    }
}

public sealed class CancelOrderDtoValidator : AbstractValidator<CancelOrderDto>
{
    public CancelOrderDtoValidator()
    {
        RuleFor(cancelOrder => cancelOrder.Reason).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
    }
}

public sealed class CreateOrderWithdrawalRequestDtoValidator : AbstractValidator<CreateOrderWithdrawalRequestDto>
{
    public CreateOrderWithdrawalRequestDtoValidator()
    {
        RuleFor(createOrderWithdrawalRequest => createOrderWithdrawalRequest.Reason).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(createOrderWithdrawalRequest => createOrderWithdrawalRequest.Reason).Must(value => value is not null && value.Trim().Length >= 10 && value.Trim().Length <= 1000).WithMessage("Az indoklás 10–1000 karakter legyen.");
    }
}

public sealed class PublicOrderWithdrawalRequestDtoValidator : AbstractValidator<PublicOrderWithdrawalRequestDto>
{
    public PublicOrderWithdrawalRequestDtoValidator()
    {
        RuleFor(publicOrderWithdrawalRequest => publicOrderWithdrawalRequest.OrderNumber).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(publicOrderWithdrawalRequest => publicOrderWithdrawalRequest.OrderNumber).MaximumLength(32).WithMessage("Legfeljebb 32 karakter adható meg.");
        RuleFor(publicOrderWithdrawalRequest => publicOrderWithdrawalRequest.OrderNumber).Must(value => value is not null && value.Length <= 32 &&
            System.Text.RegularExpressions.Regex.IsMatch(value.Trim().ToUpperInvariant(), @"\ABK-[0-9]{8}-[A-F0-9]{8}\z"))
            .WithMessage("A rendelés azonosítója nem megfelelő formátumú. Példa: BK-20261008-A1B2C3D4.");
        RuleFor(publicOrderWithdrawalRequest => publicOrderWithdrawalRequest.CustomerEmail).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(publicOrderWithdrawalRequest => publicOrderWithdrawalRequest.CustomerEmail).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(publicOrderWithdrawalRequest => publicOrderWithdrawalRequest.Reason).Must(value => value is not null && value.Trim().Length >= 10 && value.Trim().Length <= 1000).WithMessage("Az indoklás 10–1000 karakter legyen.");
    }
}

public sealed class AdminOrderUpdateDtoValidator : AbstractValidator<AdminOrderUpdateDto>
{
    public AdminOrderUpdateDtoValidator()
    {
        RuleFor(adminOrderUpdate => adminOrderUpdate.PaymentCorrectionReason).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(adminOrderUpdate => adminOrderUpdate.Status).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(adminOrderUpdate => adminOrderUpdate.PaymentStatus).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(adminOrderUpdate => adminOrderUpdate.CancellationReason).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
    }
}

public sealed class UpdateAdminInventoryDtoValidator : AbstractValidator<UpdateAdminInventoryDto>
{
    public UpdateAdminInventoryDtoValidator()
    {
        RuleFor(updateAdminInventory => updateAdminInventory.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("A készlet nem lehet negatív.");
    }
}

public sealed class AdminAccountLifecycleDecisionDtoValidator : AbstractValidator<AdminAccountLifecycleDecisionDto>
{
    public AdminAccountLifecycleDecisionDtoValidator()
    {
        RuleFor(adminAccountLifecycleDecision => adminAccountLifecycleDecision.AdminNote).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
    }
}

public sealed class HandleOrderRequestDtoValidator : AbstractValidator<HandleOrderRequestDto>
{
    public HandleOrderRequestDtoValidator()
    {
        RuleFor(handleOrderRequest => handleOrderRequest.AdminResponse).MaximumLength(1000).WithMessage("A válasz legfeljebb 1000 karakter lehet.");
    }
}
