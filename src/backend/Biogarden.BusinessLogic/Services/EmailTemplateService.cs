using System.Net;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.DTOs.Contact;
using Biogarden.BusinessLogic.DTOs.Orders;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
using Biogarden.DataAccess.Enums;
using Microsoft.Extensions.Options;

namespace Biogarden.BusinessLogic.Services;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private readonly EmailBrandingOptions _brandingOptions;

    public EmailTemplateService(IOptions<EmailBrandingOptions> brandingOptions)
    {
        _brandingOptions = brandingOptions.Value;
    }

    public EmailMessage BuildEmailConfirmationMessage(
        string recipientEmail,
        string confirmationUrl)
    {
        const string title = "Email cím megerősítése";
        const string preheader = "Erősítsd meg az email címedet a fiókod aktiválásához.";
        const string buttonText = "Email cím megerősítése";

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines:
            [
                "Köszönjük a regisztrációt a Bakony Kincse Biokertészet webshopjában.",
                "A fiókod aktiválásához kérjük, nyisd meg az alábbi linket:"
            ],
            actionText: buttonText,
            actionUrl: confirmationUrl,
            note: "Ha nem te regisztráltál, ezt az üzenetet nyugodtan figyelmen kívül hagyhatod.");

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: preheader,
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs:
            [
                "Köszönjük a regisztrációt a Bakony Kincse Biokertészet webshopjában.",
                "A fiókod aktiválásához kérjük, erősítsd meg az email címedet az alábbi gombra kattintva."
            ],
            actionText: buttonText,
            actionUrl: confirmationUrl,
            note: "Ha nem te kezdeményezted a regisztrációt, nincs további teendőd.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{title} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    public EmailMessage BuildPasswordResetMessage(
        string recipientEmail,
        string resetUrl)
    {
        const string title = "Jelszó visszaállítása";
        const string preheader = "Állítsd be az új jelszavadat a Bakony Kincse Biokertészet fiókodhoz.";
        const string buttonText = "Új jelszó beállítása";

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines:
            [
                "Jelszó-visszaállítást kértél a Bakony Kincse Biokertészet webshop fiókodhoz.",
                "Az új jelszó beállításához kérjük, nyisd meg az alábbi linket:"
            ],
            actionText: buttonText,
            actionUrl: resetUrl,
            note: "Ha nem te kérted a jelszó visszaállítását, ezt az üzenetet hagyd figyelmen kívül. A jelszavad nem változik meg.");

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: preheader,
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs:
            [
                "Jelszó-visszaállítást kértél a Bakony Kincse Biokertészet webshop fiókodhoz.",
                "Az új jelszó beállításához kattints az alábbi gombra."
            ],
            actionText: buttonText,
            actionUrl: resetUrl,
            note: "Ha nem te kérted a jelszó visszaállítását, nincs további teendőd. A jelszavad nem változik meg.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{title} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    public EmailMessage BuildPasswordChangedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(
            recipientEmail,
            title: "Jelszavad megváltozott",
            preheader: "Sikeresen megváltozott a Bakony Kincse Biokertészet fiókod jelszava.",
            bodyLines:
            [
                "Ezúton jelezzük, hogy a Bakony Kincse Biokertészet webshop fiókod jelszava sikeresen megváltozott.",
                "Ha te végezted a műveletet, nincs további teendőd."
            ],
            note: "Ha nem te kezdeményezted a jelszóváltoztatást, kérjük, minél előbb vedd fel velünk a kapcsolatot.");
    }

    public EmailMessage BuildAccountDeactivatedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(
            recipientEmail,
            title: "Fiókod deaktiválva lett",
            preheader: "A Bakony Kincse Biokertészet fiókod jelenleg nem aktív.",
            bodyLines:
            [
                "A Bakony Kincse Biokertészet webshop fiókod deaktiválva lett.",
                "A fiókba jelenleg nem lehet bejelentkezni, de az adatok megmaradnak, ezért a fiók később újraaktiválható."
            ],
            note: "Ha szeretnéd újraaktiválni a fiókodat, kérjük, vedd fel velünk a kapcsolatot.");
    }

    public EmailMessage BuildAccountReactivatedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(
            recipientEmail,
            title: "Fiókod újra aktiválva lett",
            preheader: "A Bakony Kincse Biokertészet fiókod újra használható.",
            bodyLines:
            [
                "A Bakony Kincse Biokertészet webshop fiókod újra aktiválva lett.",
                "Mostantól ismét be tudsz jelentkezni a fiókodba."
            ],
            note: "Ha nem te kérted a fiók újraaktiválását, kérjük, jelezd felénk.");
    }

    public EmailMessage BuildAccountDeletedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(
            recipientEmail,
            title: "Fiókod törölve lett",
            preheader: "A Bakony Kincse Biokertészet fiókod és mentett személyes adataid törlésre kerültek.",
            bodyLines:
            [
                "A Bakony Kincse Biokertészet webshop fiókod törlésre került.",
                "Mivel nem tartozott hozzá rendelési előzmény, a fiókot és a mentett személyes adatokat töröltük."
            ],
            note: "Ha kérdésed van a törléssel kapcsolatban, kérjük, vedd fel velünk a kapcsolatot.");
    }

    public EmailMessage BuildAccountAnonymizedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(
            recipientEmail,
            title: "Fiókadataid anonimizálva lettek",
            preheader: "A Bakony Kincse Biokertészet fiókod mentett személyes adatait anonimizáltuk.",
            bodyLines:
            [
                "A Bakony Kincse Biokertészet webshop fiókod mentett személyes adatait anonimizáltuk.",
                "A korábbi rendelési és számlázási adatok a jogszabályi megőrzési kötelezettségek miatt megmaradnak."
            ],
            note: "Az anonimizált fiók már nem használható bejelentkezésre.");
    }

    public EmailMessage BuildContactBusinessNotificationMessage(
        string recipientEmail,
        ContactMessageRequestDto request)
    {
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "Új kapcsolatfelvételi üzenet"
            : $"Új kapcsolatfelvételi üzenet: {request.Subject}";

        var plainTextBody = $"""
            Új üzenet érkezett a webshop Kapcsolat űrlapjáról.

            Név: {request.Name}
            Email: {request.Email}
            Telefon: {request.Phone ?? "Nincs megadva"}
            Tárgy: {request.Subject ?? "Nincs megadva"}

            Üzenet:
            {request.Message}
            """;

        var htmlBody = BuildHtmlBody(
            title: "Új kapcsolatfelvételi üzenet",
            preheader: $"Új üzenet érkezett: {request.Name}",
            greeting: "Új üzenet érkezett",
            bodyParagraphs:
            [
                "A webshop Kapcsolat űrlapján keresztül új megkeresés érkezett.",
                $"Név: {request.Name}",
                $"Email: {request.Email}",
                $"Telefon: {request.Phone ?? "Nincs megadva"}",
                $"Tárgy: {request.Subject ?? "Nincs megadva"}",
                $"Üzenet: {request.Message}"
            ],
            actionText: "Válasz emailben",
            actionUrl: $"mailto:{request.Email}",
            note: "A válaszhoz használd a megadott email címet, vagy keresd a látogatót telefonon, ha adott meg telefonszámot.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{subject} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    public EmailMessage BuildContactVisitorConfirmationMessage(
        string recipientEmail,
        ContactMessageRequestDto request)
    {
        const string title = "Megkaptuk az üzenetedet";

        var plainTextBody = $"""
            Kedves {request.Name}!

            Köszönjük, hogy írtál nekünk. Megkaptuk az üzenetedet, és hamarosan válaszolunk a megadott elérhetőségeden.

            Az elküldött üzeneted:
            {request.Message}

            Üdvözlettel:
            {_brandingOptions.CompanyName}

            {_brandingOptions.ContactEmail}
            {_brandingOptions.PhoneNumber}
            {_brandingOptions.Address}
            {_brandingOptions.WebsiteUrl}
            """;

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: "Köszönjük a megkeresést, hamarosan válaszolunk.",
            greeting: $"Kedves {request.Name}!",
            bodyParagraphs:
            [
                "Köszönjük, hogy írtál nekünk. Megkaptuk az üzenetedet, és hamarosan válaszolunk a megadott elérhetőségeden.",
                $"Az elküldött üzeneted: {request.Message}"
            ],
            actionText: "Webshop megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "Ez egy automatikus visszaigazolás, válaszolnod nem szükséges.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{title} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    public EmailMessage BuildOrderConfirmationMessage(string recipientEmail, OrderDto order)
    {
        const string title = "Rendelésedet megkaptuk";

        var bodyLines = new[]
        {
            "Köszönjük a rendelésedet. A rendelésedet megkaptuk, feldolgozása hamarosan megkezdődik.",
            $"Rendelésszám: {order.OrderNumber}",
            $"Rendelés állapota: {FormatOrderStatus(order.Status)}",
            $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
            $"Végösszeg: {FormatPrice(order.TotalPrice)}",
            $"Termékek: {FormatOrderItems(order)}"
        };

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: bodyLines,
            actionText: "Webshop megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelés feldolgozásáról és állapotváltozásairól emailben értesítünk.");

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: $"Megkaptuk a rendelésedet: {order.OrderNumber}.",
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs: bodyLines,
            actionText: "Webshop megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelés feldolgozásáról és állapotváltozásairól emailben értesítünk.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{title} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    public EmailMessage BuildOrderBusinessNotificationMessage(string recipientEmail, OrderDto order)
    {
        var bodyLines = new[]
        {
            "Új rendelés érkezett a webshopban.",
            $"Rendelésszám: {order.OrderNumber}",
            $"Vásárló: {order.CustomerName}",
            $"Email: {order.CustomerEmail}",
            $"Telefon: {order.CustomerPhone}",
            $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
            $"Végösszeg: {FormatPrice(order.TotalPrice)}",
            $"Termékek: {FormatOrderItems(order)}"
        };

        var plainTextBody = BuildPlainTextBody(
            greeting: "Új rendelés érkezett",
            bodyLines: bodyLines,
            actionText: "Admin felület megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelést az admin felületen tudod feldolgozni.");

        var htmlBody = BuildHtmlBody(
            title: "Új rendelés érkezett",
            preheader: $"Új rendelés érkezett: {order.OrderNumber}.",
            greeting: "Új rendelés érkezett",
            bodyParagraphs: bodyLines,
            actionText: "Admin felület megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelést az admin felületen tudod feldolgozni.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"Új rendelés érkezett - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    public EmailMessage BuildOrderStatusChangedMessage(string recipientEmail, OrderDto order)
    {
        const string title = "Rendelésed állapota frissült";

        var bodyLines = new[]
        {
            $"A rendelésed állapota megváltozott: {FormatOrderStatus(order.Status)}.",
            $"Rendelésszám: {order.OrderNumber}",
            $"Végösszeg: {FormatPrice(order.TotalPrice)}"
        };

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: bodyLines,
            actionText: "Kapcsolat",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: "Ha kérdésed van a rendeléseddel kapcsolatban, válaszolj erre az emailre vagy vedd fel velünk a kapcsolatot.");

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: $"A rendelésed új állapota: {FormatOrderStatus(order.Status)}.",
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs: bodyLines,
            actionText: "Kapcsolatfelvétel",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: "Ha kérdésed van a rendeléseddel kapcsolatban, válaszolj erre az emailre vagy vedd fel velünk a kapcsolatot.");

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{title} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    private string BuildPlainTextBody(
        string greeting,
        IReadOnlyList<string> bodyLines,
        string actionText,
        string actionUrl,
        string note)
    {
        var body = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            bodyLines);

        return $"""
            {greeting}

            {body}

            {actionText}:
            {actionUrl}

            {note}

            Üdvözlettel:
            {_brandingOptions.CompanyName}

            {_brandingOptions.ContactEmail}
            {_brandingOptions.PhoneNumber}
            {_brandingOptions.Address}
            {_brandingOptions.WebsiteUrl}
            """;
    }

    private EmailMessage BuildAccountNotificationMessage(
        string recipientEmail,
        string title,
        string preheader,
        IReadOnlyList<string> bodyLines,
        string note)
    {
        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: bodyLines,
            actionText: "Kapcsolat",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: note);

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: preheader,
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs: bodyLines,
            actionText: "Kapcsolatfelvétel",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: note);

        return new EmailMessage
        {
            To = recipientEmail,
            Subject = $"{title} - {_brandingOptions.CompanyName}",
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };
    }

    private string BuildHtmlBody(
        string title,
        string preheader,
        string greeting,
        IReadOnlyList<string> bodyParagraphs,
        string actionText,
        string actionUrl,
        string note)
    {
        var paragraphs = string.Join(
            Environment.NewLine,
            bodyParagraphs.Select(paragraph =>
                $"""<p style="margin:0 0 16px;color:#4f584b;font-size:16px;line-height:1.65;">{HtmlEncode(paragraph)}</p>"""));

        return $$"""
            <!doctype html>
            <html lang="hu">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>{{HtmlEncode(title)}}</title>
            </head>
            <body style="margin:0;padding:0;background:#f3f5ef;font-family:Arial,Helvetica,sans-serif;">
              <span style="display:none!important;visibility:hidden;opacity:0;color:transparent;height:0;width:0;overflow:hidden;">{{HtmlEncode(preheader)}}</span>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f5ef;margin:0;padding:32px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;border:1px solid #d9dfd1;border-radius:8px;overflow:hidden;">
                      <tr>
                        <td style="background:#eef3e7;padding:28px 28px 22px;text-align:center;border-bottom:1px solid #d9dfd1;">
                          <div style="font-size:13px;letter-spacing:2px;text-transform:uppercase;color:#6f8b22;font-weight:700;">{{HtmlEncode(_brandingOptions.CompanyName)}}</div>
                          <h1 style="margin:10px 0 0;color:#263124;font-size:28px;line-height:1.25;font-weight:700;">{{HtmlEncode(title)}}</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:30px 28px 26px;">
                          <p style="margin:0 0 16px;color:#263124;font-size:17px;line-height:1.65;font-weight:700;">{{HtmlEncode(greeting)}}</p>
                          {{paragraphs}}
                          <table role="presentation" cellspacing="0" cellpadding="0" style="margin:26px 0;">
                            <tr>
                              <td style="border-radius:4px;background:#6f8b22;">
                                <a href="{{HtmlAttributeEncode(actionUrl)}}" style="display:inline-block;padding:13px 22px;color:#ffffff;text-decoration:none;font-weight:700;font-size:16px;">{{HtmlEncode(actionText)}}</a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:0 0 16px;color:#4f584b;font-size:14px;line-height:1.65;">Ha a gomb nem működik, másold be ezt a linket a böngésződbe:</p>
                          <p style="margin:0 0 22px;word-break:break-all;color:#557018;font-size:13px;line-height:1.6;">{{HtmlEncode(actionUrl)}}</p>
                          <p style="margin:0;color:#65705f;font-size:14px;line-height:1.65;">{{HtmlEncode(note)}}</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="background:#f7f8f4;border-top:1px solid #d9dfd1;padding:24px 28px;">
                          {{BuildHtmlSignature()}}
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private string BuildHtmlSignature()
    {
        var logoMarkup = string.IsNullOrWhiteSpace(_brandingOptions.LogoUrl)
            ? $"""
                <div style="width:54px;height:54px;border-radius:50%;background:#eef3e7;border:1px solid #d9dfd1;color:#6f8b22;font-size:20px;font-weight:700;line-height:54px;text-align:center;">BK</div>
                """
            : $"""
                <img src="{HtmlAttributeEncode(_brandingOptions.LogoUrl)}" width="92" alt="{HtmlAttributeEncode(_brandingOptions.CompanyName)}" style="display:block;border:0;outline:none;text-decoration:none;max-width:92px;height:auto;">
                """;

        return $$"""
            <table role="presentation" cellspacing="0" cellpadding="0" width="100%">
              <tr>
                <td width="104" valign="top">{{logoMarkup}}</td>
                <td valign="top" style="padding-left:14px;">
                  <p style="margin:0 0 5px;color:#263124;font-size:16px;line-height:1.4;font-weight:700;">Üdvözlettel:<br>{{HtmlEncode(_brandingOptions.CompanyName)}}</p>
                  <p style="margin:0;color:#65705f;font-size:13px;line-height:1.6;">
                    {{HtmlEncode(_brandingOptions.ContactEmail)}}<br>
                    {{HtmlEncode(_brandingOptions.PhoneNumber)}}<br>
                    {{HtmlEncode(_brandingOptions.Address)}}<br>
                    <a href="{{HtmlAttributeEncode(_brandingOptions.WebsiteUrl)}}" style="color:#557018;text-decoration:underline;">{{HtmlEncode(_brandingOptions.WebsiteUrl)}}</a>
                  </p>
                </td>
              </tr>
            </table>
            """;
    }

    private static string HtmlEncode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string HtmlAttributeEncode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string FormatOrderItems(OrderDto order)
    {
        if (order.Items.Count == 0)
        {
            return "Nincs termékadat.";
        }

        return string.Join(
            "; ",
            order.Items.Select(item =>
                $"{item.ProductName} - {item.Quantity} {FormatUnit(item.Unit)}, {FormatPrice(item.LineTotal)}"));
    }

    private static string FormatOrderStatus(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Feldolgozás alatt",
        OrderStatus.Confirmed => "Visszaigazolva",
        OrderStatus.Shipped => "Átadásra vagy kiszállításra előkészítve",
        OrderStatus.Completed => "Teljesítve",
        OrderStatus.Cancelled => "Lemondva",
        _ => status.ToString()
    };

    private static string FormatFulfillmentMethod(FulfillmentMethod fulfillmentMethod) => fulfillmentMethod switch
    {
        FulfillmentMethod.PersonalPickup => "Személyes átvétel",
        FulfillmentMethod.HomeDelivery => "Házhozszállítás",
        FulfillmentMethod.ParcelLocker => "Csomagautomata",
        _ => fulfillmentMethod.ToString()
    };

    private static string FormatUnit(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Piece => "db",
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Gram => "g",
        UnitOfMeasure.Milliliter => "ml",
        UnitOfMeasure.Liter => "l",
        UnitOfMeasure.Package => "csomag",
        UnitOfMeasure.Bottle => "üveg",
        _ => unit.ToString()
    };

    private static string FormatPrice(decimal price) => $"{price:0.##} Ft";
}
