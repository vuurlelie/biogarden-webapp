using Biogarden.BusinessLogic.DTOs.Auth;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(loginRequest => loginRequest.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(loginRequest => loginRequest.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(loginRequest => loginRequest.Password).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(forgotPasswordRequest => forgotPasswordRequest.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(forgotPasswordRequest => forgotPasswordRequest.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
    }
}

public sealed class ResendEmailConfirmationRequestDtoValidator : AbstractValidator<ResendEmailConfirmationRequestDto>
{
    public ResendEmailConfirmationRequestDtoValidator()
    {
        RuleFor(resendEmailConfirmationRequest => resendEmailConfirmationRequest.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(resendEmailConfirmationRequest => resendEmailConfirmationRequest.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
    }
}

public sealed class ConfirmEmailRequestDtoValidator : AbstractValidator<ConfirmEmailRequestDto>
{
    public ConfirmEmailRequestDtoValidator()
    {
        RuleFor(confirmEmailRequest => confirmEmailRequest.UserId).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(confirmEmailRequest => confirmEmailRequest.Token).NotEmpty().WithMessage("A mező megadása kötelező.");
    }
}

public sealed class ResetPasswordRequestDtoValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestDtoValidator()
    {
        RuleFor(resetPasswordRequest => resetPasswordRequest.UserId).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(resetPasswordRequest => resetPasswordRequest.Token).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(resetPasswordRequest => resetPasswordRequest.Password).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(resetPasswordRequest => resetPasswordRequest.Password).MinimumLength(8).WithMessage("A jelszó legalább 8 karakter legyen.");
        RuleFor(resetPasswordRequest => resetPasswordRequest.ConfirmPassword).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(resetPasswordRequest => resetPasswordRequest.ConfirmPassword).Equal(resetPasswordRequest => resetPasswordRequest.Password).WithMessage("A jelszavak nem egyeznek.");
    }
}

public sealed class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestDtoValidator()
    {
        RuleFor(registerRequest => registerRequest.Email).Must(value => System.Net.Mail.MailAddress.TryCreate(value?.Trim(), out _)).WithMessage("Adj meg egy érvényes email címet.");
        RuleFor(registerRequest => registerRequest.Email).MaximumLength(256).WithMessage("Legfeljebb 256 karakter adható meg.");
        RuleFor(registerRequest => registerRequest.Password).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(registerRequest => registerRequest.ConfirmPassword).Equal(registerRequest => registerRequest.Password).WithMessage("A jelszavak nem egyeznek.");
        RuleFor(registerRequest => registerRequest.CustomerType).IsInEnum().WithMessage("Érvénytelen érték.");
        When(registerRequest => registerRequest.CustomerType == CustomerType.Company, () =>
        {
            RuleFor(registerRequest => registerRequest.CompanyName).NotEmpty().WithMessage("A cégnév megadása kötelező.").MaximumLength(200).WithMessage("A cégnév kötelező, legfeljebb 200 karakterrel.");
            RuleFor(registerRequest => registerRequest.TaxNumber).Must(ValidationRules.ValidTaxNumber).WithMessage("Hibás adószám. Példa: 12345678-1-12.");
            RuleFor(registerRequest => registerRequest.BillingAddress).NotNull().WithMessage("A számlázási cím megadása kötelező.");
            RuleFor(registerRequest => registerRequest.BillingAddress!).SetValidator(new AddressCreateDtoValidator()).When(registerRequest => registerRequest.BillingAddress is not null);
        });
    }
}
