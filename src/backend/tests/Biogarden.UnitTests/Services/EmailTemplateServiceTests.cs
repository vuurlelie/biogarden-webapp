using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.BusinessLogic.DTOs.Contact;
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

    [Fact]
    public void BuildPasswordResetMessage_CreatesBrandedHtmlAndPlainTextBodies()
    {
        var service = new EmailTemplateService(Options.Create(new EmailBrandingOptions
        {
            CompanyName = "Bakony Kincse BiokertÃ©szet",
            WebsiteUrl = "https://bakonykincse.hu",
            ContactEmail = "info@bakonykincse.hu",
            PhoneNumber = "+36 20 800 56 06",
            Address = "8431 BakonyszentlÃ¡szlÃ³, JÃ³kai utca 22.",
            LogoUrl = "https://bakonykincse.hu/logo.png"
        }));

        var message = service.BuildPasswordResetMessage(
            "vasarlo@example.com",
            "https://bakonykincse.hu/jelszo-visszaallitas?token=abc");

        Assert.Equal("vasarlo@example.com", message.To);
        Assert.Contains("Jelszó visszaállítása", message.Subject);
        Assert.Contains("https://bakonykincse.hu/jelszo-visszaallitas?token=abc", message.PlainTextBody);
        Assert.Contains("Új jelszó beállítása", message.PlainTextBody);
        Assert.Contains("<!doctype html>", message.HtmlBody);
        Assert.Contains("href=\"https://bakonykincse.hu/jelszo-visszaallitas?token=abc\"", message.HtmlBody);
        Assert.Contains("https://bakonykincse.hu/logo.png", message.HtmlBody);
    }

    [Fact]
    public void BuildContactBusinessNotificationMessage_CreatesNotificationForBusiness()
    {
        var service = new EmailTemplateService(Options.Create(new EmailBrandingOptions
        {
            CompanyName = "Bakony Kincse Biokertészet",
            ContactEmail = "info@bakonykincse.hu"
        }));

        var message = service.BuildContactBusinessNotificationMessage(
            "info@bakonykincse.hu",
            new ContactMessageRequestDto
            {
                Name = "Teszt Elek",
                Email = "visitor@example.com",
                Phone = "+36 20 123 4567",
                Subject = "Kérdés",
                Message = "Szeretnék érdeklődni a termékekről."
            });

        Assert.Equal("info@bakonykincse.hu", message.To);
        Assert.Contains("Új kapcsolatfelvételi üzenet", message.Subject);
        Assert.Contains("Teszt Elek", message.PlainTextBody);
        Assert.Contains("visitor@example.com", message.PlainTextBody);
        Assert.Contains("visitor@example.com", message.HtmlBody);
        Assert.Contains("mailto:visitor@example.com", message.HtmlBody);
    }

    [Fact]
    public void BuildContactVisitorConfirmationMessage_CreatesConfirmationForVisitor()
    {
        var service = new EmailTemplateService(Options.Create(new EmailBrandingOptions
        {
            CompanyName = "Bakony Kincse Biokertészet",
            WebsiteUrl = "https://bakonykincse.hu",
            ContactEmail = "info@bakonykincse.hu"
        }));

        var message = service.BuildContactVisitorConfirmationMessage(
            "visitor@example.com",
            new ContactMessageRequestDto
            {
                Name = "Teszt Elek",
                Email = "visitor@example.com",
                Message = "Szeretnék érdeklődni a termékekről."
            });

        Assert.Equal("visitor@example.com", message.To);
        Assert.Contains("Megkaptuk az üzenetedet", message.Subject);
        Assert.Contains("Kedves Teszt Elek!", message.PlainTextBody);
        Assert.Contains("https://bakonykincse.hu", message.HtmlBody);
    }
}
