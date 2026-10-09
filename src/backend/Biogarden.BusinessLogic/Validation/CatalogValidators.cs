using Biogarden.BusinessLogic.DTOs.Products;
using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.DataAccess.Enums;
using FluentValidation;

namespace Biogarden.BusinessLogic.Validation;

public sealed class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(productCreate => productCreate.Name).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(productCreate => productCreate.Name).Length(2, 200).WithMessage("A név 2–200 karakter legyen.");
        RuleFor(productCreate => productCreate.Description).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
        RuleFor(productCreate => productCreate.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(productCreate => productCreate.Price).InclusiveBetween(0, 10000000).WithMessage("Az ár 0 és 10 000 000 Ft között lehet.");
        RuleFor(productCreate => productCreate.Category).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(productCreate => productCreate.Unit).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(productCreate => productCreate.UnitAmount).InclusiveBetween(0.01m, 10000000m).WithMessage("Érvénytelen kiszerelés.");
        RuleFor(productCreate => productCreate.UnitAmountMeasure).IsInEnum().WithMessage("Érvénytelen mértékegység.");
        RuleFor(productCreate => productCreate.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("A készlet nem lehet negatív.");
    }
}

public sealed class ProductEditDtoValidator : AbstractValidator<ProductEditDto>
{
    public ProductEditDtoValidator()
    {
        RuleFor(productEdit => productEdit.Name).NotEmpty().WithMessage("A mező megadása kötelező.");
        RuleFor(productEdit => productEdit.Name).Length(2, 200).WithMessage("A név 2–200 karakter legyen.");
        RuleFor(productEdit => productEdit.Description).MaximumLength(1000).WithMessage("Legfeljebb 1000 karakter adható meg.");
        RuleFor(productEdit => productEdit.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(productEdit => productEdit.Price).InclusiveBetween(0, 10000000).WithMessage("Az ár 0 és 10 000 000 Ft között lehet.");
        RuleFor(productEdit => productEdit.Category).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(productEdit => productEdit.Unit).IsInEnum().WithMessage("Érvénytelen érték.");
        RuleFor(productEdit => productEdit.UnitAmount).InclusiveBetween(0.01m, 10000000m).WithMessage("Érvénytelen kiszerelés.");
        RuleFor(productEdit => productEdit.UnitAmountMeasure).IsInEnum().WithMessage("Érvénytelen mértékegység.");
        RuleFor(productEdit => productEdit.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("A készlet nem lehet negatív.");
    }
}

public sealed class PostCreateDtoValidator : AbstractValidator<PostCreateDto>
{
    public PostCreateDtoValidator()
    {
        RuleFor(postCreate => postCreate.Title).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 3).WithMessage("A cím legalább 3 karakter legyen.");
        RuleFor(postCreate => postCreate.Title).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(postCreate => postCreate.Article).MaximumLength(ArticleHtml.MaximumLength).WithMessage("A HTML szöveg túl hosszú.");
        RuleFor(postCreate => postCreate.Article).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 20).WithMessage("A szöveg legalább 20 karakter legyen.");
        RuleFor(postCreate => postCreate.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(postCreate => postCreate.Category).IsInEnum().WithMessage("Érvénytelen érték.");
    }
}

public sealed class PostEditDtoValidator : AbstractValidator<PostEditDto>
{
    public PostEditDtoValidator()
    {
        RuleFor(postEdit => postEdit.Title).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 3).WithMessage("A cím legalább 3 karakter legyen.");
        RuleFor(postEdit => postEdit.Title).MaximumLength(200).WithMessage("Legfeljebb 200 karakter adható meg.");
        RuleFor(postEdit => postEdit.Article).MaximumLength(ArticleHtml.MaximumLength).WithMessage("A HTML szöveg túl hosszú.");
        RuleFor(postEdit => postEdit.Article).Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 20).WithMessage("A szöveg legalább 20 karakter legyen.");
        RuleFor(postEdit => postEdit.Image).MaximumLength(500).WithMessage("Legfeljebb 500 karakter adható meg.");
        RuleFor(postEdit => postEdit.Category).IsInEnum().WithMessage("Érvénytelen érték.");
    }
}
