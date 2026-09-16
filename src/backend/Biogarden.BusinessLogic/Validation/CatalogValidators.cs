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

public sealed class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Name).Length(2, 200).WithMessage("A név 2–200 karakter legyen.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
        RuleFor(x => x.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(x => x.Price).InclusiveBetween(0, 10000000).WithMessage("Az ár 0 és 10 000 000 Ft között lehet.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.Unit).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.UnitAmount).InclusiveBetween(0.01m, 10000000m).WithMessage("Érvénytelen kiszerelés.");
        RuleFor(x => x.UnitAmountMeasure).IsInEnum().WithMessage("Érvénytelen mértékegység.");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("A készlet nem lehet negatív.");
    }
}

public sealed class ProductEditDtoValidator : AbstractValidator<ProductEditDto>
{
    public ProductEditDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(x => x.Name).Length(2, 200).WithMessage("A név 2–200 karakter legyen.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
        RuleFor(x => x.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(x => x.Price).InclusiveBetween(0, 10000000).WithMessage("Az ár 0 és 10 000 000 Ft között lehet.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.Unit).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(x => x.UnitAmount).InclusiveBetween(0.01m, 10000000m).WithMessage("Érvénytelen kiszerelés.");
        RuleFor(x => x.UnitAmountMeasure).IsInEnum().WithMessage("Érvénytelen mértékegység.");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("A készlet nem lehet negatív.");
    }
}

public sealed class PostCreateDtoValidator : AbstractValidator<PostCreateDto>
{
    public PostCreateDtoValidator()
    {
        RuleFor(x => x.Title).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 3).WithMessage("A cím legalább 3 karakter legyen.");
        RuleFor(x => x.Title).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(x => x.Article).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 20).WithMessage("A szöveg legalább 20 karakter legyen.");
        RuleFor(x => x.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Érvénytelen érték.");
    }
}

public sealed class PostEditDtoValidator : AbstractValidator<PostEditDto>
{
    public PostEditDtoValidator()
    {
        RuleFor(x => x.Title).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 3).WithMessage("A cím legalább 3 karakter legyen.");
        RuleFor(x => x.Title).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(x => x.Article).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 20).WithMessage("A szöveg legalább 20 karakter legyen.");
        RuleFor(x => x.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Érvénytelen érték.");
    }
}
