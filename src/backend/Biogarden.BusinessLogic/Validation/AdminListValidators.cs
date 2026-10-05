using Biogarden.BusinessLogic.DTOs.Admin;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public abstract class AdminListValidator<T> : AbstractValidator<T> where T : AdminListQuery
{
    protected AdminListValidator() => RuleFor(x => x.Search).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
}

public sealed class AdminOrderListValidator : AdminListValidator<AdminOrderListQuery>
{
    public AdminOrderListValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("Érvénytelen szűrő.");
        RuleFor(x => x.Fulfillment).IsInEnum().WithMessage("Érvénytelen szűrő.");
    }
}
public sealed class AdminUserListValidator : AdminListValidator<AdminUserListQuery>
{
    public AdminUserListValidator()
    {
        RuleFor(x => x.Role).Must(x => x is null or "all" or "admin" or "customer").WithMessage("Érvénytelen szerepkörszűrő.");
        RuleFor(x => x.Status).Must(x => x is null or "all" or "active" or "inactive" or "request").WithMessage("Érvénytelen állapotszűrő.");
    }
}
public sealed class AdminInventoryListValidator : AdminListValidator<AdminInventoryListQuery>
{
    public AdminInventoryListValidator() => RuleFor(x => x.Stock).Must(x => x is null or "all" or "low" or "empty" or "reserved").WithMessage("Érvénytelen készletszűrő.");
}
public sealed class AdminWithdrawalListValidator : AdminListValidator<AdminWithdrawalListQuery>
{
    public AdminWithdrawalListValidator() => RuleFor(x => x.Status).IsInEnum().WithMessage("Érvénytelen szűrő.");
}
