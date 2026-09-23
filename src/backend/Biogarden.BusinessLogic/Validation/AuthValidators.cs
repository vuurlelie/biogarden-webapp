using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(x => x.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(x => x.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
    }
}

public sealed class ResendEmailConfirmationRequestDtoValidator : AbstractValidator<ResendEmailConfirmationRequestDto>
{
    public ResendEmailConfirmationRequestDtoValidator()
    {
        RuleFor(x => x.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(x => x.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
    }
}

public sealed class ConfirmEmailRequestDtoValidator : AbstractValidator<ConfirmEmailRequestDto>
{
    public ConfirmEmailRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Token).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class ResetPasswordRequestDtoValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestDtoValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Token).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Password).MinimumLength(8).WithMessage("A jelszó legalább 8 karakter legyen.");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("A jelszavak nem egyeznek.");
    }
}

public sealed class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(x => x.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("A jelszavak nem egyeznek.");
        RuleFor(x => x.CustomerType).IsInEnum().WithMessage("Érvénytelen érték.");
        When(x => x.CustomerType == CustomerType.Company, () =>
        {
            RuleFor(x => x.CompanyName).NotEmpty().WithMessage("A cégnév megadása kötelező.").MaximumLength(200).WithMessage("A cégnév kötelező, legfeljebb 200 karakterrel.");
            RuleFor(x => x.TaxNumber).Must(ValidationRules.ValidTaxNumber).WithMessage("Hibás adószám. Példa: 12345678-1-12.");
            RuleFor(x => x.BillingAddress).NotNull().WithMessage("A számlázási cím megadása kötelező.");
            RuleFor(x => x.BillingAddress!).SetValidator(new AddressCreateDtoValidator()).When(x => x.BillingAddress is not null);
        });
    }
}
