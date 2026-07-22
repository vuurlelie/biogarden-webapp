using Biogarden.BusinessLogic.Options;
using Biogarden.BusinessLogic.Services;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.DataAccess.Enums;
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
        Assert.Contains("fontos értesítéseket", message.PlainTextBody);
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
            CompanyName = "Bakony Kincse Biokertészet",
            WebsiteUrl = "https://bakonykincse.hu",
            ContactEmail = "info@bakonykincse.hu",
            PhoneNumber = "+36 20 800 56 06",
            Address = "8431 Bakonyszentlászló, Jókai utca 22.",
            LogoUrl = "https://bakonykincse.hu/logo.png"
        }));

        var message = service.BuildPasswordResetMessage(
            "vasarlo@example.com",
            "https://bakonykincse.hu/jelszo-visszaallitas?token=abc");

        Assert.Equal("vasarlo@example.com", message.To);
        Assert.Contains("Jelszó visszaállítása", message.Subject);
        Assert.Contains("https://bakonykincse.hu/jelszo-visszaallitas?token=abc", message.PlainTextBody);
        Assert.Contains("Új jelszó beállítása", message.PlainTextBody);
        Assert.Contains("A jelszavad csak akkor változik meg", message.PlainTextBody);
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
        Assert.DoesNotContain("Üdvözlettel:", message.PlainTextBody);
        Assert.DoesNotContain("https://bakonykincse.hu/logo.png", message.HtmlBody);
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
                Phone = "+36 20 123 4567",
                Message = "Szeretnék érdeklődni a termékekről."
            });

        Assert.Equal("visitor@example.com", message.To);
        Assert.Contains("Megkaptuk az üzenetedet", message.Subject);
        Assert.Contains("Kedves Teszt Elek!", message.PlainTextBody);
        Assert.Contains("Megadott adataid:", message.PlainTextBody);
        Assert.Contains("- Név: Teszt Elek", message.PlainTextBody);
        Assert.Contains("- Email: visitor@example.com", message.PlainTextBody);
        Assert.Contains("- Telefon: +36 20 123 4567", message.PlainTextBody);
        Assert.Contains("https://bakonykincse.hu", message.HtmlBody);
        Assert.Contains("Megadott adataid:", message.HtmlBody);
        Assert.Contains("<br>", message.HtmlBody);
    }

    [Fact]
    public void BuildContactVisitorConfirmationMessage_WhenPhoneIsMissing_OmitsPhoneFromSubmittedDetails()
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

        Assert.Contains("Megadott adataid:", message.PlainTextBody);
        Assert.Contains("- Email: visitor@example.com", message.PlainTextBody);
        Assert.DoesNotContain("- Telefon:", message.PlainTextBody);
    }

    [Fact]
    public void BuildOrderConfirmationMessage_CreatesCustomerOrderSummary()
    {
        var service = CreateOrderTemplateService();
        var order = CreateOrderDto();

        var message = service.BuildOrderConfirmationMessage("customer@example.com", order);

        Assert.Equal("customer@example.com", message.To);
        Assert.Contains("Rendelésedet megkaptuk", message.Subject);
        Assert.Contains("BK-TEST-0001", message.PlainTextBody);
        Assert.Contains("Fizetés állapota: Fizetésre vár", message.PlainTextBody);
        Assert.Contains("Vevő adatai:", message.PlainTextBody);
        Assert.Contains("- Név: Teszt Elek", message.PlainTextBody);
        Assert.Contains("- Email: customer@example.com", message.PlainTextBody);
        Assert.Contains("- Telefon: +36 20 123 4567", message.PlainTextBody);
        Assert.Contains("Számlázási adatok:", message.PlainTextBody);
        Assert.Contains("- Cím: 2112 Veresegyház Szemere Pál utca 25", message.PlainTextBody);
        Assert.Contains("Átvételi adatok:", message.PlainTextBody);
        Assert.Contains("- Személyes átvétel", message.PlainTextBody);
        Assert.Contains("Levendula", message.PlainTextBody);
        Assert.Contains("Termékek:", message.PlainTextBody);
        Assert.Contains("- Levendula - 2 db, 3600 Ft", message.PlainTextBody);
        Assert.Contains("Weboldal megnyitása", message.PlainTextBody);
        Assert.Contains("<!doctype html>", message.HtmlBody);
        Assert.Contains("BK-TEST-0001", message.HtmlBody);
        Assert.Contains("<br>", message.HtmlBody);
    }

    [Fact]
    public void BuildOrderConfirmationMessage_WhenDeliveryIsSelected_IncludesDeliveryAddress()
    {
        var service = CreateOrderTemplateService();
        var order = CreateOrderDto();
        order.FulfillmentMethod = FulfillmentMethod.HomeDelivery;
        order.DeliveryPostalCode = "8431";
        order.DeliveryCity = "Bakonyszentlászló";
        order.DeliveryStreet = "Jókai utca";
        order.DeliveryHouseNumber = "22";
        order.DeliveryFloor = "1";
        order.DeliveryDoor = "3";

        var message = service.BuildOrderConfirmationMessage("customer@example.com", order);

        Assert.Contains("Szállítási adatok:", message.PlainTextBody);
        Assert.Contains(
            "- Cím: 8431 Bakonyszentlászló Jókai utca 22 (emelet: 1, ajtó: 3)",
            message.PlainTextBody);
        Assert.DoesNotContain("Átvételi adatok:", message.PlainTextBody);
    }

    [Fact]
    public void BuildOrderBusinessNotificationMessage_CreatesBusinessOrderSummary()
    {
        var service = CreateOrderTemplateService();
        var order = CreateOrderDto();

        var message = service.BuildOrderBusinessNotificationMessage("info@bakonykincse.hu", order);

        Assert.Equal("info@bakonykincse.hu", message.To);
        Assert.Contains("Új rendelés érkezett", message.Subject);
        Assert.Matches(
            "A rendelést az admin felületen tudod feldolgozni\\.[\\s\\S]*Admin felület megnyitása:",
            message.PlainTextBody);
        Assert.Contains("Vevő adatai:", message.PlainTextBody);
        Assert.Contains("- Név: Teszt Elek", message.PlainTextBody);
        Assert.Contains("- Email: customer@example.com", message.PlainTextBody);
        Assert.Contains("- Telefon: +36 20 123 4567", message.PlainTextBody);
        Assert.Contains("Termékek:", message.PlainTextBody);
        Assert.Contains("- Levendula - 2 db, 3600 Ft", message.PlainTextBody);
        Assert.Contains("Teszt Elek", message.PlainTextBody);
        Assert.Contains("customer@example.com", message.PlainTextBody);
        Assert.Contains("customer@example.com", message.HtmlBody);
        Assert.Contains("Vevő adatai:", message.HtmlBody);
        Assert.Contains("<br>", message.HtmlBody);
        Assert.Contains("href=\"https://bakonykincse.hu/admin\"", message.HtmlBody);
        Assert.Contains("https://bakonykincse.hu/admin", message.PlainTextBody);
        Assert.DoesNotContain("Üdvözlettel:", message.PlainTextBody);
        Assert.DoesNotContain("https://bakonykincse.hu/logo.png", message.HtmlBody);
    }

    [Fact]
    public void BuildOrderStatusChangedMessage_CreatesCustomerStatusUpdate()
    {
        var service = CreateOrderTemplateService();
        var order = CreateOrderDto();
        order.Status = OrderStatus.Confirmed;

        var message = service.BuildOrderStatusChangedMessage("customer@example.com", order);

        Assert.Equal("customer@example.com", message.To);
        Assert.Contains("Rendelésed állapota frissült", message.Subject);
        Assert.Contains("Visszaigazolva", message.PlainTextBody);
        Assert.Contains("Fizetés állapota", message.PlainTextBody);
        Assert.Contains("Átvétel módja", message.PlainTextBody);
        Assert.Contains("mailto:info@bakonykincse.hu", message.HtmlBody);
    }

    [Theory]
    [InlineData("password", "Jelszavad megváltozott")]
    [InlineData("deactivated", "Fiókod deaktiválva lett")]
    [InlineData("reactivated", "Fiókod újra aktiválva lett")]
    [InlineData("deleted", "Fiókod törölve lett")]
    [InlineData("anonymized", "Fiókadataid anonimizálva lettek")]
    public void BuildAccountLifecycleMessages_CreateBrandedNotifications(
        string messageType,
        string expectedTitle)
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

        var message = messageType switch
        {
            "password" => service.BuildPasswordChangedMessage("user@example.com"),
            "deactivated" => service.BuildAccountDeactivatedMessage("user@example.com"),
            "reactivated" => service.BuildAccountReactivatedMessage("user@example.com"),
            "deleted" => service.BuildAccountDeletedMessage("user@example.com"),
            "anonymized" => service.BuildAccountAnonymizedMessage("user@example.com"),
            _ => throw new ArgumentOutOfRangeException(nameof(messageType))
        };

        Assert.Equal("user@example.com", message.To);
        Assert.Contains(expectedTitle, message.Subject);
        Assert.Contains("Kedves Felhasználónk!", message.PlainTextBody);
        Assert.Contains("info@bakonykincse.hu", message.PlainTextBody);
        Assert.Contains("<!doctype html>", message.HtmlBody);
        Assert.Contains("mailto:info@bakonykincse.hu", message.HtmlBody);
        Assert.Contains("https://bakonykincse.hu/logo.png", message.HtmlBody);
    }

    private static EmailTemplateService CreateOrderTemplateService()
    {
        return new EmailTemplateService(Options.Create(new EmailBrandingOptions
        {
            CompanyName = "Bakony Kincse Biokertészet",
            WebsiteUrl = "https://bakonykincse.hu",
            ContactEmail = "info@bakonykincse.hu",
            PhoneNumber = "+36 20 800 56 06",
            Address = "8431 Bakonyszentlászló, Jókai utca 22.",
            LogoUrl = "https://bakonykincse.hu/logo.png"
        }));
    }

    private static OrderDto CreateOrderDto()
    {
        return new OrderDto
        {
            Id = Guid.NewGuid(),
            OrderNumber = "BK-TEST-0001",
            CustomerName = "Teszt Elek",
            CustomerEmail = "customer@example.com",
            CustomerPhone = "+36 20 123 4567",
            PostalCode = "2112",
            City = "Veresegyház",
            Street = "Szemere Pál utca",
            HouseNumber = "25",
            TotalPrice = 3600,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            FulfillmentMethod = FulfillmentMethod.PersonalPickup,
            Items =
            [
                new OrderItemDto
                {
                    ProductId = 1,
                    ProductName = "Levendula",
                    Quantity = 2,
                    Unit = UnitOfMeasure.Piece,
                    UnitPrice = 1800,
                    LineTotal = 3600
                }
            ]
        };
    }
}
