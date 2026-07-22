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
                "Köszönjük a regisztrációt a Bakony Kincse Biokertészet weboldalán.",
                "A fiókod aktiválásához kérjük, nyisd meg az alábbi linket:",
                "A megerősítés után erre az email címre küldjük a fiókodhoz kapcsolódó fontos értesítéseket."
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
                "Köszönjük a regisztrációt a Bakony Kincse Biokertészet weboldalán.",
                "A fiókod aktiválásához kérjük, erősítsd meg az email címedet az alábbi gombra kattintva.",
                "A megerősítés után erre az email címre küldjük a fiókodhoz kapcsolódó fontos értesítéseket."
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
                "Jelszó-visszaállítást kértél a Bakony Kincse Biokertészet fiókodhoz.",
                "Az új jelszó beállításához kérjük, nyisd meg az alábbi linket:",
                "A jelszavad csak akkor változik meg, ha ezen a linken új jelszót állítasz be."
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
                "Jelszó-visszaállítást kértél a Bakony Kincse Biokertészet fiókodhoz.",
                "Az új jelszó beállításához kattints az alábbi gombra.",
                "A jelszavad csak akkor változik meg, ha ezen a linken új jelszót állítasz be."
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
                "Ezúton jelezzük, hogy a Bakony Kincse Biokertészet fiókod jelszava sikeresen megváltozott.",
                "Ha te végezted a műveletet, nincs további teendőd."
            ],
            note: "Ha nem te kezdeményezted a jelszóváltoztatást, kérj jelszó-visszaállítást, és minél előbb vedd fel velünk a kapcsolatot.");
    }

    public EmailMessage BuildAccountDeactivatedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(
            recipientEmail,
            title: "Fiókod deaktiválva lett",
            preheader: "A Bakony Kincse Biokertészet fiókod jelenleg nem aktív.",
            bodyLines:
            [
                "A Bakony Kincse Biokertészet fiókod deaktiválva lett.",
                "A fiókba jelenleg nem lehet bejelentkezni, de az adatok megmaradnak, ezért a fiók később újraaktiválható.",
                "Ha folyamatban lévő rendelésed van, annak teljesítéséről továbbra is egyeztetünk."
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
                "A Bakony Kincse Biokertészet fiókod újra aktiválva lett.",
                "Mostantól ismét be tudsz jelentkezni a fiókodba.",
                "A korábban mentett adataid a fiókodban ismét elérhetők."
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
                "A Bakony Kincse Biokertészet fiókod törlésre került.",
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
                "A Bakony Kincse Biokertészet fiókod mentett személyes adatait anonimizáltuk.",
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
            Új üzenet érkezett a weboldal Kapcsolat űrlapjáról.

            Név: {request.Name}
            Email: {request.Email}
            Telefon: {request.Phone ?? "Nincs megadva"}
            Tárgy: {request.Subject ?? "Nincs megadva"}

            Üzenet:
            {request.Message}
            """;

        var htmlBody = BuildInternalHtmlBody(
            title: "Új kapcsolatfelvételi üzenet",
            preheader: $"Új üzenet érkezett: {request.Name}",
            greeting: "Új üzenet érkezett",
            bodyParagraphs:
            [
                "A weboldal Kapcsolat űrlapján keresztül új megkeresés érkezett.",
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
        var submittedDetails = FormatContactSubmittedDetails(request);

        var plainTextBody = $"""
            Kedves {request.Name}!

            Köszönjük, hogy írtál nekünk. Megkaptuk az üzenetedet, és hamarosan válaszolunk a megadott elérhetőségeden.

            {submittedDetails}

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
                submittedDetails,
                $"Az elküldött üzeneted: {request.Message}"
            ],
            actionText: "Weboldal megnyitása",
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
            $"Fizetés állapota: {FormatPaymentStatus(order.PaymentStatus)}",
            $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
            FormatCustomerDetails(order),
            FormatCustomerBillingDocumentDetails(order),
            FormatDeliveryDetails(order),
            $"Végösszeg: {FormatPrice(order.TotalPrice)}",
            FormatOrderItemsList(order)
        };

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: bodyLines,
            actionText: "Weboldal megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelés feldolgozásáról és állapotváltozásairól emailben értesítünk.");

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: $"Megkaptuk a rendelésedet: {order.OrderNumber}.",
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs: bodyLines,
            actionText: "Weboldal megnyitása",
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
        var adminUrl = BuildAdminDashboardUrl();
        var bodyLines = new[]
        {
            "Új rendelés érkezett a weboldalon.",
            "A rendelést az admin felületen tudod feldolgozni.",
            $"Rendelésszám: {order.OrderNumber}",
            FormatCustomerDetails(order),
            FormatAdminBillingDocumentDetails(order),
            $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
            $"Végösszeg: {FormatPrice(order.TotalPrice)}",
            FormatOrderItemsList(order)
        };

        var plainTextBody = BuildInternalPlainTextBody(
            greeting: "Új rendelés érkezett",
            bodyLines: bodyLines,
            actionText: "Admin felület megnyitása",
            actionUrl: adminUrl,
            note: "Ha a gomb nem működik, használd a fenti admin linket.");

        var htmlBody = BuildInternalHtmlBody(
            title: "Új rendelés érkezett",
            preheader: $"Új rendelés érkezett: {order.OrderNumber}.",
            greeting: "Új rendelés érkezett",
            bodyParagraphs: bodyLines,
            actionText: "Admin felület megnyitása",
            actionUrl: adminUrl,
            note: "Ha a gomb nem működik, használd a fenti admin linket.");

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
            $"Fizetés állapota: {FormatPaymentStatus(order.PaymentStatus)}",
            $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
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

    private static string BuildInternalPlainTextBody(
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
                $"""<p style="margin:0 0 16px;color:#4f584b;font-size:16px;line-height:1.65;">{FormatHtmlParagraph(paragraph)}</p>"""));

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

    private static string BuildInternalHtmlBody(
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
                $"""<p style="margin:0 0 16px;color:#4f584b;font-size:16px;line-height:1.65;">{FormatHtmlParagraph(paragraph)}</p>"""));

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
                        <td style="padding:30px 28px 26px;">
                          <h1 style="margin:0 0 18px;color:#263124;font-size:26px;line-height:1.25;font-weight:700;">{{HtmlEncode(greeting)}}</h1>
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

    private static string FormatHtmlParagraph(string value)
    {
        return HtmlEncode(value)
            .Replace(Environment.NewLine, "<br>", StringComparison.Ordinal);
    }

    private string BuildAdminDashboardUrl()
    {
        return $"{_brandingOptions.WebsiteUrl.TrimEnd('/')}/admin";
    }

    private static string FormatCustomerDetails(OrderDto order)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "Vevő adatai:",
            $"- Név: {order.CustomerName}",
            $"- Email: {order.CustomerEmail}",
            $"- Telefon: {order.CustomerPhone}"
        });
    }

    private static string FormatBillingDetails(OrderDto order)
    {
        var lines = new List<string>
        {
            "Számlázási adatok:"
        };

        if (!string.IsNullOrWhiteSpace(order.BillingCompanyName))
        {
            lines.Add($"- Cégnév: {order.BillingCompanyName}");
        }

        if (!string.IsNullOrWhiteSpace(order.BillingTaxNumber))
        {
            lines.Add($"- Adószám: {order.BillingTaxNumber}");
        }

        lines.Add($"- Cím: {FormatAddress(
            order.PostalCode,
            order.City,
            order.Street,
            order.HouseNumber,
            order.Building,
            order.Floor,
            order.Door,
            order.Doorbell)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCustomerBillingDocumentDetails(OrderDto order)
    {
        if (order.CustomerType == CustomerType.Private)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Bizonylat:",
                "- Magánszemélyként a rendeléshez papíralapú nyugtát mellékelünk."
            });
        }

        var invoiceMode = order.PrintedInvoiceRequested
            ? "Elektronikus számlát küldünk emailben, és nyomtatott számlát is mellékelünk."
            : "Elektronikus számlát küldünk emailben.";

        return string.Join(Environment.NewLine, new[]
        {
            "Számlázási adatok:",
            $"- Cégnév: {order.BillingCompanyName ?? "Nincs megadva"}",
            $"- Adószám: {order.BillingTaxNumber ?? "Nincs megadva"}",
            $"- Cím: {FormatAddress(
                order.PostalCode,
                order.City,
                order.Street,
                order.HouseNumber,
                order.Building,
                order.Floor,
                order.Door,
                order.Doorbell)}",
            $"- Számla átvétele: {invoiceMode}"
        });
    }

    private static string FormatAdminBillingDocumentDetails(OrderDto order)
    {
        if (order.CustomerType == CustomerType.Private)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Kiállítandó bizonylat:",
                "- Papíralapú nyugta szükséges.",
                "- A nyugtát mellékeld a rendeléshez."
            });
        }

        var invoiceTask = order.PrintedInvoiceRequested
            ? "Elektronikus számla szükséges, és nyomtatott számlát is mellékelni kell."
            : "Elektronikus számla szükséges.";

        return string.Join(Environment.NewLine, new[]
        {
            "Kiállítandó bizonylat:",
            $"- Admin teendő: {invoiceTask}",
            $"- Cégnév: {order.BillingCompanyName ?? "Nincs megadva"}",
            $"- Adószám: {order.BillingTaxNumber ?? "Nincs megadva"}",
            $"- Számlázási cím: {FormatAddress(
                order.PostalCode,
                order.City,
                order.Street,
                order.HouseNumber,
                order.Building,
                order.Floor,
                order.Door,
                order.Doorbell)}"
        });
    }

    private static string FormatDeliveryDetails(OrderDto order)
    {
        if (order.FulfillmentMethod == FulfillmentMethod.PersonalPickup)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Átvételi adatok:",
                "- Személyes átvétel"
            });
        }

        return string.Join(Environment.NewLine, new[]
        {
            "Szállítási adatok:",
            $"- Cím: {FormatAddress(
                order.DeliveryPostalCode,
                order.DeliveryCity,
                order.DeliveryStreet,
                order.DeliveryHouseNumber,
                order.DeliveryBuilding,
                order.DeliveryFloor,
                order.DeliveryDoor,
                order.DeliveryDoorbell)}"
        });
    }

    private static string FormatAddress(
        string? postalCode,
        string? city,
        string? street,
        string? houseNumber,
        string? building,
        string? floor,
        string? door,
        string? doorbell)
    {
        var streetLine = string.Join(
            " ",
            new[] { street, houseNumber }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var mainAddress = string.Join(
            " ",
            new[] { postalCode, city, streetLine }.Where(value => !string.IsNullOrWhiteSpace(value)));

        var extras = new List<string>();

        if (!string.IsNullOrWhiteSpace(building))
        {
            extras.Add($"épület: {building}");
        }

        if (!string.IsNullOrWhiteSpace(floor))
        {
            extras.Add($"emelet: {floor}");
        }

        if (!string.IsNullOrWhiteSpace(door))
        {
            extras.Add($"ajtó: {door}");
        }

        if (!string.IsNullOrWhiteSpace(doorbell))
        {
            extras.Add($"kapucsengő: {doorbell}");
        }

        if (extras.Count == 0)
        {
            return string.IsNullOrWhiteSpace(mainAddress) ? "Nincs megadva" : mainAddress;
        }

        return $"{mainAddress} ({string.Join(", ", extras)})";
    }

    private static string FormatOrderItemsList(OrderDto order)
    {
        if (order.Items.Count == 0)
        {
            return string.Join(Environment.NewLine, new[]
            {
                "Termékek:",
                "- Nincs termékadat."
            });
        }

        return string.Join(
            Environment.NewLine,
            new[] { "Termékek:" }.Concat(order.Items.Select(item =>
                $"- {item.ProductName} - {item.Quantity} {FormatUnit(item.Unit)}, {FormatPrice(item.LineTotal)}")));
    }

    private static string FormatContactSubmittedDetails(ContactMessageRequestDto request)
    {
        var lines = new List<string>
        {
            "Megadott adataid:",
            $"- Név: {request.Name}",
            $"- Email: {request.Email}"
        };

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            lines.Add($"- Telefon: {request.Phone}");
        }

        return string.Join(Environment.NewLine, lines);
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

    private static string FormatPaymentStatus(PaymentStatus paymentStatus) => paymentStatus switch
    {
        PaymentStatus.Pending => "Fizetésre vár",
        PaymentStatus.Authorized => "Engedélyezve",
        PaymentStatus.Paid => "Fizetve",
        PaymentStatus.Failed => "Sikertelen",
        PaymentStatus.RefundPending => "Visszatérítés folyamatban",
        PaymentStatus.Refunded => "Visszatérítve",
        PaymentStatus.PartiallyRefunded => "Részben visszatérítve",
        _ => paymentStatus.ToString()
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
