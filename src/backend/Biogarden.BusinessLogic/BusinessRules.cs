namespace Biogarden.BusinessLogic;

public static class BusinessRules
{
    public const int MaximumPendingOrderRequests = 3;

    public const int PostTitleMaxLength = 200;
    public const int PostImageMaxLength = 1000;
    public const int PostArticleMaxLength = 10000;
    public const string DefaultPostImagePath = "/uploads/posts/defaultimg.jpg";

    public const int ProductNameMaxLength = 200;
    public const int ProductDescriptionMaxLength = 1000;
    public const int ProductImageMaxLength = 500;
    public const int ProductPackageSizeMaxLength = 100;
    public const string DefaultProductImagePath = "/uploads/products/defaultimg.png";
}
