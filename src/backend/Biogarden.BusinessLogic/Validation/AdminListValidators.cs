using Biogarden.BusinessLogic.DTOs.Admin;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public abstract class AdminListValidator<T> : AbstractValidator<T> where T : AdminListQuery
{
    protected AdminListValidator() => RuleFor(query => query.Search).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
}

public sealed class AdminOrderListValidator : AdminListValidator<AdminOrderListQuery>
{
    public AdminOrderListValidator()
    {
        RuleFor(adminOrderListQuery => adminOrderListQuery.Status).IsInEnum().WithMessage("Érvénytelen szűrő.");
        RuleFor(adminOrderListQuery => adminOrderListQuery.Fulfillment).IsInEnum().WithMessage("Érvénytelen szűrő.");
    }
}
public sealed class AdminUserListValidator : AdminListValidator<AdminUserListQuery>
{
    public AdminUserListValidator()
    {
        RuleFor(adminUserListQuery => adminUserListQuery.Role).Must(roleFilter => roleFilter is null or "all" or "admin" or "customer").WithMessage("Érvénytelen szerepkörszűrő.");
        RuleFor(adminUserListQuery => adminUserListQuery.Status).Must(statusFilter => statusFilter is null or "all" or "active" or "inactive" or "request").WithMessage("Érvénytelen állapotszűrő.");
    }
}
public sealed class AdminInventoryListValidator : AdminListValidator<AdminInventoryListQuery>
{
    public AdminInventoryListValidator() => RuleFor(adminInventoryListQuery => adminInventoryListQuery.Stock).Must(stockFilter => stockFilter is null or "all" or "low" or "empty" or "reserved").WithMessage("Érvénytelen készletszűrő.");
}
public sealed class AdminWithdrawalListValidator : AdminListValidator<AdminWithdrawalListQuery>
{
    public AdminWithdrawalListValidator() => RuleFor(adminWithdrawalListQuery => adminWithdrawalListQuery.Status).IsInEnum().WithMessage("Érvénytelen szűrő.");
}
