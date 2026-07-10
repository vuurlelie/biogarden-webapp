using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Microsoft.Extensions.Options;

namespace Biogarden.UnitTests.Services;

public sealed class EmailTemplateServiceTests
{
    [Fact]
    public void BuildEmailConfirmationMessage_CreatesBrandedHtmlAndPlainTextBodies()
    {
        var service = new EmailTemplateService(Options.Create(new EmailBrandingOptions
        {
            CompanyName = "Bakony Kincse Biokertészet",
            WebsiteUrl = "https://bakonykincse.hu",
            ContactEmail = "info@bakonykincse.hu",
            PhoneNumber = "+36 20 800 56 06",
            Address = "8431 Bakonyszentlászló, Jókai utca 22.",
            LogoUrl = "https://bakonykincse.hu/logo.png"
        }));

        var message = service.BuildEmailConfirmationMessage(
            "vasarlo@example.com",
            "https://bakonykincse.hu/email-megerosites?token=abc");

        Assert.Equal("vasarlo@example.com", message.To);
        Assert.Contains("Email cím megerősítése", message.Subject);
        Assert.Contains("Bakony Kincse Biokertészet", message.Subject);
        Assert.Contains("https://bakonykincse.hu/email-megerosites?token=abc", message.PlainTextBody);
        Assert.Contains("Üdvözlettel:", message.PlainTextBody);
        Assert.Contains("info@bakonykincse.hu", message.PlainTextBody);
        Assert.Contains("<!doctype html>", message.HtmlBody);
        Assert.Contains("href=\"https://bakonykincse.hu/email-megerosites?token=abc\"", message.HtmlBody);
        Assert.Contains("https://bakonykincse.hu/logo.png", message.HtmlBody);
        Assert.Contains("info@bakonykincse.hu", message.HtmlBody);
    }

    [Fact]
    public void BuildEmailConfirmationMessage_WhenLogoUrlIsMissing_UsesTextLogoInSignature()
    {
        var service = new EmailTemplateService(Options.Create(new EmailBrandingOptions
        {
            LogoUrl = string.Empty
        }));

        var message = service.BuildEmailConfirmationMessage(
            "vasarlo@example.com",
            "https://bakonykincse.hu/email-megerosites?token=abc");

        Assert.Contains(">BK<", message.HtmlBody);
    }
}
