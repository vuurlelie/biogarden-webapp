namespace Biogarden.BusinessLogic.Options;

public sealed class EmailBrandingOptions
{
    public const string SectionName = "EmailBranding";

    public string CompanyName { get; init; } = "Bakony Kincse Biokertészet";
    public string WebsiteUrl { get; init; } = "https://bakonykincse.hu";
    public string ContactEmail { get; init; } = "info@bakonykincse.hu";
    public string PhoneNumber { get; init; } = "+36 20 800 56 06";
    public string Address { get; init; } = "8431 Bakonyszentlászló, Jókai utca 22.";
    public string LogoUrl { get; init; } = string.Empty;
}
