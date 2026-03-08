namespace Biogarden.BusinessLogic;

public static class BusinessRules
{
    public const int PostTitleMaxLength = 200;
    public const int PostImageMaxLength = 1000;
    public const int PostArticleMaxLength = 10000;
    public const string DefaultPostImagePath = "/images/posts/default.png";

    public const int ProductNameMaxLength = 200;
    public const int ProductDescriptionMaxLength = 1000;
    public const int ProductImageMaxLength = 500;
    public const int ProductPackageSizeMaxLength = 100;
    public const string DefaultProductImagePath = "/images/products/default.png";

    public const int AddressPostalCodeLength = 4;
    public const int AddressCityMaxLength = 100;
    public const int AddressStreetMaxLength = 150;
    public const int AddressHouseNumberMaxLength = 20;
    public const int AddressBuildingMaxLength = 20;
    public const int AddressFloorMaxLength = 20;
    public const int AddressDoorMaxLength = 20;
    public const int AddressDoorbellMaxLength = 50;
}