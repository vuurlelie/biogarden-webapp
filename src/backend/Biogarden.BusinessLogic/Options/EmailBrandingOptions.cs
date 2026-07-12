using System.Net.Mail;

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

    public void ValidateForStartup()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            throw new InvalidOperationException(
                "EmailBranding:CompanyName is required.");
        }

        if (!Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var websiteUri)
            || (websiteUri.Scheme != Uri.UriSchemeHttp && websiteUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "EmailBranding:WebsiteUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (!MailAddress.TryCreate(ContactEmail, out _))
        {
            throw new InvalidOperationException(
                "EmailBranding:ContactEmail must be a valid email address.");
        }

        if (!string.IsNullOrWhiteSpace(LogoUrl)
            && (!Uri.TryCreate(LogoUrl, UriKind.Absolute, out var logoUri)
                || (logoUri.Scheme != Uri.UriSchemeHttp && logoUri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException(
                "EmailBranding:LogoUrl must be empty or an absolute HTTP or HTTPS URL.");
        }
    }
}
