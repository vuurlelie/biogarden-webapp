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
    private static readonly TimeZoneInfo BudapestTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");

    private static string FormatBudapestTime(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), BudapestTimeZone)
            .ToString("yyyy. MM. dd. HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private readonly EmailBrandingOptions _brandingOptions;

    public EmailTemplateService(IOptions<EmailBrandingOptions> brandingOptions)
    {
        _brandingOptions = brandingOptions.Value;
    }

    public EmailMessage BuildEmailConfirmationMessage(string recipientEmail, string confirmationUrl)
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
            note: "Ha nem te regisztráltál, ezt az üzenetet nyugodtan figyelmen kívül hagyhatod."
        );

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
            note: "Ha nem te kezdeményezted a regisztrációt, nincs további teendőd."
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(title), plainTextBody, htmlBody);
    }

    public EmailMessage BuildPasswordResetMessage(string recipientEmail, string resetUrl)
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
            note: "Ha nem te kérted a jelszó visszaállítását, ezt az üzenetet hagyd figyelmen kívül. A jelszavad nem változik meg."
        );

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
            note: "Ha nem te kérted a jelszó visszaállítását, nincs további teendőd. A jelszavad nem változik meg."
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(title), plainTextBody, htmlBody);
    }

    public EmailMessage BuildPasswordChangedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(recipientEmail, BuildPasswordChangedContent());
    }

    public EmailMessage BuildAccountDeactivatedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(recipientEmail, BuildAccountDeactivatedContent());
    }

    public EmailMessage BuildAccountReactivatedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(recipientEmail, BuildAccountReactivatedContent());
    }

    public EmailMessage BuildAccountDeletedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(recipientEmail, BuildAccountDeletedContent());
    }

    public EmailMessage BuildAccountAnonymizedMessage(string recipientEmail)
    {
        return BuildAccountNotificationMessage(recipientEmail, BuildAccountAnonymizedContent());
    }

    public EmailMessage BuildContactBusinessNotificationMessage(string recipientEmail, ContactMessageRequestDto request)
    {
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "Új kapcsolatfelvételi üzenet"
            : $"Új kapcsolatfelvételi üzenet: {request.Subject}";

        var bodyLines = BuildContactBusinessBodyLines(request);
        var plainTextBody = BuildContactBusinessPlainTextBody(request);

        var htmlBody = BuildInternalHtmlBody(
            title: "Új kapcsolatfelvételi üzenet",
            preheader: $"Új üzenet érkezett: {request.Name}",
            greeting: "Új üzenet érkezett",
            bodyParagraphs:
            [
                "A weboldal Kapcsolat űrlapján keresztül új megkeresés érkezett."
            ],
            actionText: "Válasz emailben",
            actionUrl: $"mailto:{request.Email}",
            note: "A válaszhoz használd a megadott email címet, vagy keresd a látogatót telefonon, ha adott meg telefonszámot.",
            extraHtmlContent: BuildContactBusinessHtmlSummary(request)
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(subject), plainTextBody, htmlBody);
    }

    public EmailMessage BuildContactVisitorConfirmationMessage(string recipientEmail, ContactMessageRequestDto request)
    {
        const string title = "Megkaptuk az üzenetedet";
        var submittedDetails = FormatContactSubmittedDetails(request);
        var bodyLines = BuildContactVisitorBodyLines(request, submittedDetails);
        var plainTextBody = BuildContactVisitorPlainTextBody(request, submittedDetails);

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: "Köszönjük a megkeresést, hamarosan válaszolunk.",
            greeting: $"Kedves {request.Name}!",
            bodyParagraphs:
            [
                "Köszönjük, hogy írtál nekünk. Megkaptuk az üzenetedet, és hamarosan válaszolunk a megadott elérhetőségeden."
            ],
            actionText: "Weboldal megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "Ez egy automatikus visszaigazolás, válaszolnod nem szükséges.",
            extraHtmlContent: BuildContactVisitorHtmlSummary(request)
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(title), plainTextBody, htmlBody);
    }

    public EmailMessage BuildOrderConfirmationMessage(string recipientEmail, OrderDto order)
    {
        const string title = "Rendelésedet megkaptuk";
        var bodyLines = BuildOrderConfirmationBodyLines(order);

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: bodyLines,
            actionText: "Weboldal megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelés feldolgozásáról és állapotváltozásairól emailben értesítünk."
        );

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: $"Megkaptuk a rendelésedet: {order.OrderNumber}.",
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs:
            [
                "Köszönjük a rendelésedet. A rendelésedet megkaptuk, feldolgozása hamarosan megkezdődik."
            ],
            actionText: "Weboldal megnyitása",
            actionUrl: _brandingOptions.WebsiteUrl,
            note: "A rendelés feldolgozásáról és állapotváltozásairól emailben értesítünk.",
            extraHtmlContent: BuildOrderConfirmationHtmlSummary(order)
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(title), plainTextBody, htmlBody);
    }

    public EmailMessage BuildOrderBusinessNotificationMessage(string recipientEmail, OrderDto order)
    {
        var adminUrl = BuildAdminDashboardUrl();
        var bodyLines = BuildOrderBusinessBodyLines(order);

        var plainTextBody = BuildInternalPlainTextBody(
            greeting: "Új rendelés érkezett",
            bodyLines: bodyLines,
            actionText: "Admin felület megnyitása",
            actionUrl: adminUrl,
            note: "Ellenőrizd az átvételi módot, a fizetési módot és a kiállítandó bizonylat típusát."
        );

        var htmlBody = BuildInternalHtmlBody(
            title: "Új rendelés érkezett",
            preheader: $"Új rendelés érkezett: {order.OrderNumber}.",
            greeting: "Új rendelés érkezett",
            bodyParagraphs:
            [
                "Új rendelés érkezett a weboldalon.",
                "A rendelést az admin felületen tudod feldolgozni."
            ],
            actionText: "Admin felület megnyitása",
            actionUrl: adminUrl,
            note: "Ellenőrizd az átvételi módot, a fizetési módot és a kiállítandó bizonylat típusát.",
            extraHtmlContent: BuildOrderBusinessHtmlSummary(order)
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject("Új rendelés érkezett"), plainTextBody, htmlBody);
    }

    public EmailMessage BuildOrderStatusChangedMessage(string recipientEmail, OrderDto order)
    {
        const string title = "Rendelésed állapota frissült";
        var bodyLines = BuildOrderStatusChangedBodyLines(order);

        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: bodyLines,
            actionText: "Kapcsolat",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: "Ha kérdésed van a rendeléseddel kapcsolatban, válaszolj erre az emailre vagy vedd fel velünk a kapcsolatot."
        );

        var htmlBody = BuildHtmlBody(
            title: title,
            preheader: $"A rendelésed új állapota: {FormatOrderStatus(order.Status)}.",
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs:
            [
                $"A rendelésed állapota megváltozott: {FormatOrderStatus(order.Status)}."
            ],
            actionText: "Kapcsolatfelvétel",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: "Ha kérdésed van a rendeléseddel kapcsolatban, válaszolj erre az emailre vagy vedd fel velünk a kapcsolatot.",
            extraHtmlContent: BuildOrderStatusChangedHtmlSummary(order)
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(title), plainTextBody, htmlBody);
    }

    public EmailMessage BuildOrderWithdrawalBusinessNotificationMessage(string recipientEmail, OrderDto order, OrderWithdrawalRequestDto request)
    {
        var adminUrl = BuildAdminDashboardUrl();
        var bodyLines = BuildWithdrawalBusinessBodyLines(order, request);

        return CreateMessage(
            recipientEmail,
            BuildBrandedSubject($"Elállási vagy fogyasztói kérés - {order.OrderNumber}"),
            BuildInternalPlainTextBody(
                            greeting: "Új kérés érkezett",
                            bodyLines: bodyLines,
                            actionText: "Admin felület megnyitása",
                            actionUrl: adminUrl,
                            note: "A kérést az admin felületen, a rendelés részleteinél látod."
                        ),
            BuildInternalHtmlBody(
                        title: "Új elállási vagy fogyasztói kérés",
                        preheader: $"Új kérés érkezett: {order.OrderNumber}.",
                        greeting: "Új kérés érkezett",
                        bodyParagraphs:
                        [
                            "Új elállási vagy fogyasztói kérés érkezett."
                        ],
                        actionText: "Admin felület megnyitása",
                        actionUrl: adminUrl,
                        note: "A kérést az admin felületen, a rendelés részleteinél látod.",
                        extraHtmlContent: BuildWithdrawalBusinessHtmlSummary(order, request)
                    )
        );
    }

    public EmailMessage BuildOrderWithdrawalCustomerConfirmationMessage(string recipientEmail, OrderDto order, OrderWithdrawalRequestDto request)
    {
        var bodyLines = BuildWithdrawalCustomerBodyLines(order, request);

        return CreateMessage(
            recipientEmail,
            BuildBrandedSubject($"Megkaptuk a rendeléshez kapcsolódó kérésedet - {order.OrderNumber}"),
            BuildPlainTextBody(
                            greeting: $"Kedves {order.CustomerName}!",
                            bodyLines: bodyLines,
                            actionText: "Kapcsolat",
                            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
                            note: "Ez az email a kérés rögzítését igazolja, nem jelenti automatikusan annak elfogadását."
                        ),
            BuildHtmlBody(
                        title: "Megkaptuk a kérésedet",
                        preheader: $"Rögzítettük a(z) {order.OrderNumber} rendeléshez kapcsolódó kérésedet.",
                        greeting: $"Kedves {order.CustomerName}!",
                        bodyParagraphs:
                        [
                            $"Megkaptuk a(z) {order.OrderNumber} számú rendeléshez kapcsolódó kérésedet.",
                            "A kérésedet rögzítettük, és a feldolgozásáról a megadott elérhetőségen egyeztetünk."
                        ],
                        actionText: "Kapcsolat",
                        actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
                        note: "Ez az email a kérés rögzítését igazolja, nem jelenti automatikusan annak elfogadását.",
                        extraHtmlContent: BuildWithdrawalCustomerHtmlSummary(order, request)
                    )
        );
    }

    private string BuildPlainTextBody(string greeting, IReadOnlyList<string> bodyLines, string actionText, string actionUrl, string note)
    {
        var body = string.Join($"{Environment.NewLine}{Environment.NewLine}", bodyLines);

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

    private static string BuildInternalPlainTextBody(string greeting, IReadOnlyList<string> bodyLines, string actionText, string actionUrl, string note)
    {
        var body = string.Join($"{Environment.NewLine}{Environment.NewLine}", bodyLines);

        return $"""
            {greeting}

            {body}

            {actionText}:
            {actionUrl}

            {note}
            """;
    }

    private static AccountNotificationContent BuildPasswordChangedContent() =>
        new(
            Title: "Jelszavad megváltozott",
            Preheader: "Sikeresen megváltozott a Bakony Kincse Biokertészet fiókod jelszava.",
            BodyLines:
            [
                "Ezúton jelezzük, hogy a Bakony Kincse Biokertészet fiókod jelszava sikeresen megváltozott.",
                "Ha te végezted a műveletet, nincs további teendőd."
            ],
            Note: "Ha nem te kezdeményezted a jelszóváltoztatást, kérj jelszó-visszaállítást, és minél előbb vedd fel velünk a kapcsolatot."
        );

    private static AccountNotificationContent BuildAccountDeactivatedContent() =>
        new(
            Title: "Fiókod deaktiválva lett",
            Preheader: "A Bakony Kincse Biokertészet fiókod jelenleg nem aktív.",
            BodyLines:
            [
                "A Bakony Kincse Biokertészet fiókod deaktiválva lett.",
                "A fiókba jelenleg nem lehet bejelentkezni, de az adatok megmaradnak, ezért a fiók később újraaktiválható.",
                "Ha folyamatban lévő rendelésed van, annak teljesítéséről továbbra is egyeztetünk."
            ],
            Note: "Ha szeretnéd újraaktiválni a fiókodat, kérjük, vedd fel velünk a kapcsolatot."
        );

    private static AccountNotificationContent BuildAccountReactivatedContent() =>
        new(
            Title: "Fiókod újra aktiválva lett",
            Preheader: "A Bakony Kincse Biokertészet fiókod újra használható.",
            BodyLines:
            [
                "A Bakony Kincse Biokertészet fiókod újra aktiválva lett.",
                "Mostantól ismét be tudsz jelentkezni a fiókodba.",
                "A korábban mentett adataid a fiókodban ismét elérhetők."
            ],
            Note: "Ha nem te kérted a fiók újraaktiválását, kérjük, jelezd felénk."
        );

    private static AccountNotificationContent BuildAccountDeletedContent() =>
        new(
            Title: "Fiókod törölve lett",
            Preheader: "A Bakony Kincse Biokertészet fiókod és mentett személyes adataid törlésre kerültek.",
            BodyLines:
            [
                "A Bakony Kincse Biokertészet fiókod törlésre került.",
                "Mivel nem tartozott hozzá rendelési előzmény, a fiókot és a mentett személyes adatokat töröltük."
            ],
            Note: "Ha kérdésed van a törléssel kapcsolatban, kérjük, vedd fel velünk a kapcsolatot."
        );

    private static AccountNotificationContent BuildAccountAnonymizedContent() =>
        new(
            Title: "Fiókadataid anonimizálva lettek",
            Preheader: "A Bakony Kincse Biokertészet fiókod mentett személyes adatait anonimizáltuk.",
            BodyLines:
            [
                "A Bakony Kincse Biokertészet fiókod mentett személyes adatait anonimizáltuk.",
                "A korábbi rendelési és számlázási adatok a jogszabályi megőrzési kötelezettségek miatt megmaradnak."
            ],
            Note: "Az anonimizált fiók már nem használható bejelentkezésre."
        );

    private EmailMessage BuildAccountNotificationMessage(string recipientEmail, AccountNotificationContent content)
    {
        var plainTextBody = BuildPlainTextBody(
            greeting: "Kedves Felhasználónk!",
            bodyLines: content.BodyLines,
            actionText: "Kapcsolat",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: content.Note
        );

        var htmlBody = BuildHtmlBody(
            title: content.Title,
            preheader: content.Preheader,
            greeting: "Kedves Felhasználónk!",
            bodyParagraphs: content.BodyLines,
            actionText: "Kapcsolatfelvétel",
            actionUrl: $"mailto:{_brandingOptions.ContactEmail}",
            note: content.Note
        );

        return CreateMessage(recipientEmail, BuildBrandedSubject(content.Title), plainTextBody, htmlBody);
    }

    private sealed record AccountNotificationContent(string Title, string Preheader, IReadOnlyList<string> BodyLines, string Note);

    private EmailMessage CreateMessage(string recipientEmail, string subject, string plainTextBody, string htmlBody) =>
        new()
        {
            To = recipientEmail,
            Subject = subject,
            PlainTextBody = plainTextBody,
            HtmlBody = htmlBody
        };

    private string BuildBrandedSubject(string subject) =>
        $"{subject} - {_brandingOptions.CompanyName}";

    private string BuildHtmlBody(
        string title,
        string preheader,
        string greeting,
        IReadOnlyList<string> bodyParagraphs,
        string actionText,
        string actionUrl,
        string note,
        string? extraHtmlContent = null
    )
    {
        var paragraphs = string.Join(
            Environment.NewLine,
            bodyParagraphs.Select(paragraph =>
                $"""<p style="margin:0 0 16px;color:#4f584b;font-size:16px;line-height:1.65;">{FormatHtmlParagraph(paragraph)}</p>""")
        );

        return $$"""
            <!doctype html>
            <html lang="hu">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>{{HtmlEncode(title)}}</title>
              <style>
                a[x-apple-data-detectors] { color: inherit !important; text-decoration: none !important; }
                .email-data-text { color: #263124 !important; text-decoration: none !important; }
              </style>
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
                          {{extraHtmlContent ?? string.Empty}}
                          <table role="presentation" cellspacing="0" cellpadding="0" style="margin:26px 0;">
                            <tr>
                              <td style="border-radius:4px;background:#6f8b22;">
                                <a href="{{HtmlAttributeEncode(actionUrl)}}" style="display:inline-block;padding:13px 22px;color:#ffffff;text-decoration:none;font-weight:700;font-size:16px;">{{HtmlEncode(actionText)}}</a>
                              </td>
                            </tr>
                          </table>
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

    private static IEnumerable<(string, string)> PurchaseRows(OrderDto order)
    {
        yield return ("Fizetési mód", order.PaymentMethod switch
        {
            OrderPaymentMethod.OnlineCard => "Online bankkártyás fizetés",
            OrderPaymentMethod.PayAtPickup => "Fizetés átvételkor",
            OrderPaymentMethod.CashOnDelivery => "Utánvét",
            _ => "Korábbi rendelés – nincs rögzítve"
        });
        if (order.Currency is not null)
            yield return ("Pénznem", order.Currency);
        if (order.Subtotal.HasValue)
            yield return ("Termékek összege", FormatPrice(order.Subtotal.Value));
        if (order.DeliveryFee.HasValue)
            yield return ("Szállítási díj", FormatPrice(order.DeliveryFee.Value));
        if (order.PaymentFee.HasValue)
            yield return ("Fizetési díj", FormatPrice(order.PaymentFee.Value));
        if (order.Subtotal is null || order.DeliveryFee is null || order.PaymentFee is null)
            yield return ("Díjbontás", "A korábbi rendelés díjbontása nem áll rendelkezésre.");
        if (order.GlsDeliveryPointId is not null)
            yield return ("GLS átvételi pont", $"{order.GlsDeliveryPointName} ({order.GlsDeliveryPointId}) – {order.GlsDeliveryPointCountryCode} {order.DeliveryPostalCode} {order.DeliveryCity}, {order.GlsDeliveryPointAddress}");
    }

    private static string FormatPurchaseDetails(OrderDto order)
    {
        var details = string.Join(Environment.NewLine, PurchaseRows(order).Select(row => $"{row.Item1}: {row.Item2}"));
        var mapUrl = GlsMapUrl(order);
        return mapUrl is null ? details : $"{details}{Environment.NewLine}GLS pont a térképen: {mapUrl}";
    }

    private static string BuildOrderConfirmationHtmlSummary(OrderDto order)
    {
        var orderSummaryRows = new[]
        {
            ("Rendelésszám", order.OrderNumber),
            ("Rendelés állapota", FormatOrderStatus(order.Status)),
            ("Fizetés állapota", FormatPaymentStatus(order.PaymentStatus)),
            ("Átvétel módja", FormatFulfillmentMethod(order.FulfillmentMethod)),
            ("Végösszeg", FormatPrice(order.TotalPrice))
        };

        var customerRows = new[]
        {
            ("Név", order.CustomerName),
            ("Email", order.CustomerEmail),
            ("Telefon", order.CustomerPhone)
        };

        var documentRows = order.CustomerType == CustomerType.Private
            ? new[]
            {
                ("Bizonylat", "Papíralapú nyugtát mellékelünk a rendeléshez.")
            }
            : new[]
            {
                ("Cégnév", order.BillingCompanyName ?? "Nincs megadva"),
                ("Adószám", order.BillingTaxNumber ?? "Nincs megadva"),
                ("Számlázási cím", FormatAddress(order.PostalCode, order.City, order.Street, order.HouseNumber, order.Building, order.Floor, order.Door, order.Doorbell)),
                ("Számla átvétele", order.PrintedInvoiceRequested
                    ? "Elektronikus számlát küldünk emailben, és nyomtatott számlát is mellékelünk."
                    : "Elektronikus számlát küldünk emailben.")
            };

        var fulfillmentRows = order.FulfillmentMethod == FulfillmentMethod.PersonalPickup
            ? new[]
            {
                ("Átvétel", "Személyes átvétel")
            }
            : new[]
            {
                ("Szállítási cím", FormatAddress(
                    order.DeliveryPostalCode,
                    order.DeliveryCity,
                    order.DeliveryStreet,
                    order.DeliveryHouseNumber,
                    order.DeliveryBuilding,
                    order.DeliveryFloor,
                    order.DeliveryDoor,
                    order.DeliveryDoorbell
                ))
            };

        return string.Join(
            Environment.NewLine,
            new[]
            {
                BuildHtmlSection("Rendelés összefoglaló", BuildHtmlDetailsTable(orderSummaryRows.Concat(PurchaseRows(order)).ToArray(), GlsMapUrl(order))),
                BuildHtmlSection("Megadott adataid", BuildHtmlDetailsTable(customerRows)),
                BuildHtmlSection(order.CustomerType == CustomerType.Private ? "Bizonylat" : "Számlázási adatok", BuildHtmlDetailsTable(documentRows)),
                BuildHtmlSection(order.FulfillmentMethod == FulfillmentMethod.PersonalPickup ? "Átvételi adatok" : "Szállítási adatok", BuildHtmlDetailsTable(fulfillmentRows, GlsMapUrl(order))),
                BuildHtmlSection("Termékek", BuildHtmlOrderItemsTable(order))
            });
    }

    private static string BuildOrderBusinessHtmlSummary(OrderDto order)
    {
        var orderSummaryRows = new[]
        {
            ("Rendelésszám", order.OrderNumber),
            ("Rendelés állapota", FormatOrderStatus(order.Status)),
            ("Fizetés állapota", FormatPaymentStatus(order.PaymentStatus)),
            ("Átvétel módja", FormatFulfillmentMethod(order.FulfillmentMethod)),
            ("Végösszeg", FormatPrice(order.TotalPrice))
        };

        var customerRows = new[]
        {
            ("Név", order.CustomerName),
            ("Email", order.CustomerEmail),
            ("Telefon", order.CustomerPhone)
        };

        var documentRows = order.CustomerType == CustomerType.Private
            ? new[]
            {
                ("Teendő", "Papíralapú nyugta szükséges."),
                ("Megjegyzés", "A nyugtát mellékeld a rendeléshez.")
            }
            : new[]
            {
                ("Teendő", order.PrintedInvoiceRequested
                    ? "Elektronikus számla szükséges, és nyomtatott számlát is mellékelni kell."
                    : "Elektronikus számla szükséges."),
                ("Cégnév", order.BillingCompanyName ?? "Nincs megadva"),
                ("Adószám", order.BillingTaxNumber ?? "Nincs megadva"),
                ("Számlázási cím", FormatAddress(order.PostalCode, order.City, order.Street, order.HouseNumber, order.Building, order.Floor, order.Door, order.Doorbell))
            };

        return string.Join(
            Environment.NewLine,
            new[]
            {
                BuildHtmlSection("Rendelés összefoglaló", BuildHtmlDetailsTable(orderSummaryRows.Concat(PurchaseRows(order)).ToArray(), GlsMapUrl(order))),
                BuildHtmlSection("Vevő adatai", BuildHtmlDetailsTable(customerRows)),
                BuildHtmlSection("Kiállítandó bizonylat", BuildHtmlDetailsTable(documentRows)),
                BuildHtmlSection("Átvételi adatok", BuildHtmlDetailsTable(new[] { ("Átvétel", FormatDeliveryDetails(order)) }, GlsMapUrl(order))),
                BuildHtmlSection("Termékek", BuildHtmlOrderItemsTable(order))
            });
    }

    private static string BuildOrderStatusChangedHtmlSummary(OrderDto order) =>
        BuildHtmlSection(
            "Rendelés adatai",
            BuildHtmlDetailsTable(
            [
                ("Rendelésszám", order.OrderNumber),
                ("Rendelés állapota", FormatOrderStatus(order.Status)),
                ("Fizetés állapota", FormatPaymentStatus(order.PaymentStatus)),
                ("Átvétel módja", FormatFulfillmentMethod(order.FulfillmentMethod)),
                ("Végösszeg", FormatPrice(order.TotalPrice)),
                .. PurchaseRows(order)
            ], GlsMapUrl(order))
        );

    private static string BuildContactBusinessHtmlSummary(ContactMessageRequestDto request) =>
        string.Join(
            Environment.NewLine,
            new[]
            {
                BuildHtmlSection(
                    "Megadott adatok",
                    BuildHtmlDetailsTable(
                    [
                        ("Név", request.Name),
                        ("Email", request.Email),
                        ("Telefon", request.Phone ?? "Nincs megadva"),
                        ("Tárgy", request.Subject ?? "Nincs megadva")
                    ])
                ),
                BuildHtmlSection("Üzenet", BuildHtmlDetailsTable([("Üzenet", request.Message)]))
            });

    private static string BuildContactVisitorHtmlSummary(ContactMessageRequestDto request)
    {
        var rows = new List<(string Label, string Value)>
        {
            ("Név", request.Name),
            ("Email", request.Email)
        };

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            rows.Add(("Telefon", request.Phone));
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                BuildHtmlSection("Megadott adataid", BuildHtmlDetailsTable(rows)),
                BuildHtmlSection("Elküldött üzeneted", BuildHtmlDetailsTable([("Üzenet", request.Message)]))
            });
    }

    private static string BuildWithdrawalBusinessHtmlSummary(OrderDto order, OrderWithdrawalRequestDto request) =>
        BuildHtmlSection(
            "Kérés adatai",
            BuildHtmlDetailsTable(
            [
                ("Rendelésszám", order.OrderNumber),
                ("Vevő", request.CustomerName),
                ("Email", request.CustomerEmail),
                ("Kérés ideje", $"{FormatBudapestTime(request.RequestedAtUtc)}"),
                ("Üzenet", request.Reason)
            ])
        );

    private static string BuildWithdrawalCustomerHtmlSummary(OrderDto order, OrderWithdrawalRequestDto request) =>
        BuildHtmlSection(
            "Rögzített kérés",
            BuildHtmlDetailsTable(
            [
                ("Rendelésszám", order.OrderNumber),
                ("Kérés ideje", $"{FormatBudapestTime(request.RequestedAtUtc)}"),
                ("Megadott üzenet", request.Reason)
            ])
        );

    private static string BuildHtmlSection(string title, string content) =>
        $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin:18px 0 0;">
          <tr>
            <td style="padding:0 0 8px;">
              <h2 style="margin:0;color:#263124;font-size:18px;line-height:1.35;font-weight:700;">{HtmlEncode(title)}</h2>
            </td>
          </tr>
          <tr>
            <td>
              {content}
            </td>
          </tr>
        </table>
        """;

    private static string? GlsMapUrl(OrderDto order)
    {
        if (string.IsNullOrWhiteSpace(order.GlsDeliveryPointId))
            return null;

        var address = $"{order.DeliveryPostalCode} {order.DeliveryCity}, {order.GlsDeliveryPointAddress}, {order.GlsDeliveryPointCountryCode}";
        return "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(address);
    }

    private static string DetailValueHtml(string label, string value, string? glsMapUrl)
    {
        var text = HtmlEncode(value);
        return glsMapUrl is not null && label is "GLS átvételi pont" or "Szállítási cím" or "Átvétel"
            ? $"<a href=\"{HtmlAttributeEncode(glsMapUrl)}\" style=\"color:#557018;text-decoration:underline;\">{text}</a>"
            : text;
    }

    private static string BuildHtmlDetailsTable(IEnumerable<(string Label, string Value)> rows, string? glsMapUrl = null)
    {
        var htmlRows = string.Join(
            Environment.NewLine,
            rows.Select(row =>
                $"""
                <tr>
                  <td style="width:38%;padding:10px 12px;border-bottom:1px solid #d9dfd1;color:#65705f;font-size:14px;line-height:1.45;font-weight:700;vertical-align:top;"><span class="email-data-text" style="color:#65705f;text-decoration:none;">{HtmlEncode(row.Label)}</span></td>
                  <td style="padding:10px 12px;border-bottom:1px solid #d9dfd1;color:#263124;font-size:14px;line-height:1.45;vertical-align:top;"><span class="email-data-text" style="color:#263124;text-decoration:none;">{DetailValueHtml(row.Label, row.Value, glsMapUrl)}</span></td>
                </tr>
                """));

        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #d9dfd1;border-radius:6px;background:#f7f8f4;border-collapse:separate;border-spacing:0;overflow:hidden;">
              {htmlRows}
            </table>
            """;
    }

    private static string BuildHtmlOrderItemsTable(OrderDto order)
    {
        if (order.Items.Count == 0)
        {
            return BuildHtmlDetailsTable(
            [
                ("Termékek", "Nincs termékadat.")
            ]);
        }

        var itemRows = string.Join(
            Environment.NewLine,
            order.Items.Select(item =>
                $"""
                <tr>
                  <td style="padding:10px 12px;border-bottom:1px solid #d9dfd1;color:#263124;font-size:14px;line-height:1.45;vertical-align:top;">{HtmlEncode(item.ProductName)}</td>
                  <td align="center" style="padding:10px 12px;border-bottom:1px solid #d9dfd1;color:#4f584b;font-size:14px;line-height:1.45;vertical-align:top;">{item.Quantity} {HtmlEncode(FormatUnit(item.Unit))}</td>
                  <td align="right" style="padding:10px 12px;border-bottom:1px solid #d9dfd1;color:#4f584b;font-size:14px;line-height:1.45;vertical-align:top;">{HtmlEncode(FormatPrice(item.UnitPrice))}</td>
                  <td align="right" style="padding:10px 12px;border-bottom:1px solid #d9dfd1;color:#263124;font-size:14px;line-height:1.45;font-weight:700;vertical-align:top;">{HtmlEncode(FormatPrice(item.LineTotal))}</td>
                </tr>
                """));

        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #d9dfd1;border-radius:6px;background:#ffffff;border-collapse:separate;border-spacing:0;overflow:hidden;">
              <tr>
                <th align="left" style="padding:10px 12px;background:#eef3e7;border-bottom:1px solid #d9dfd1;color:#263124;font-size:13px;line-height:1.35;text-transform:uppercase;">Termék</th>
                <th align="center" style="padding:10px 12px;background:#eef3e7;border-bottom:1px solid #d9dfd1;color:#263124;font-size:13px;line-height:1.35;text-transform:uppercase;">Mennyiség</th>
                <th align="right" style="padding:10px 12px;background:#eef3e7;border-bottom:1px solid #d9dfd1;color:#263124;font-size:13px;line-height:1.35;text-transform:uppercase;">Egységár</th>
                <th align="right" style="padding:10px 12px;background:#eef3e7;border-bottom:1px solid #d9dfd1;color:#263124;font-size:13px;line-height:1.35;text-transform:uppercase;">Összeg</th>
              </tr>
              {itemRows}
              <tr>
                <td colspan="4" style="padding:0;border-top:1px solid #d9dfd1;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                    <tr>
                      <td align="left" style="padding:12px;color:#263124;font-size:15px;line-height:1.4;font-weight:700;">Végösszeg</td>
                      <td align="right" style="padding:12px;color:#263124;font-size:15px;line-height:1.4;font-weight:700;white-space:nowrap;min-width:120px;">{HtmlEncode(FormatPrice(order.TotalPrice))}</td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            """;
    }

    private static string BuildInternalHtmlBody(
        string title,
        string preheader,
        string greeting,
        IReadOnlyList<string> bodyParagraphs,
        string actionText,
        string actionUrl,
        string note,
        string? extraHtmlContent = null
    )
    {
        var paragraphs = string.Join(
            Environment.NewLine,
            bodyParagraphs.Select(paragraph =>
                $"""<p style="margin:0 0 16px;color:#4f584b;font-size:16px;line-height:1.65;">{FormatHtmlParagraph(paragraph)}</p>""")
        );

        return $$"""
            <!doctype html>
            <html lang="hu">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>{{HtmlEncode(title)}}</title>
              <style>
                a[x-apple-data-detectors] { color: inherit !important; text-decoration: none !important; }
                .email-data-text { color: #263124 !important; text-decoration: none !important; }
              </style>
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
                          {{extraHtmlContent ?? string.Empty}}
                          <table role="presentation" cellspacing="0" cellpadding="0" style="margin:26px 0;">
                            <tr>
                              <td style="border-radius:4px;background:#6f8b22;">
                                <a href="{{HtmlAttributeEncode(actionUrl)}}" style="display:inline-block;padding:13px 22px;color:#ffffff;text-decoration:none;font-weight:700;font-size:16px;">{{HtmlEncode(actionText)}}</a>
                              </td>
                            </tr>
                          </table>
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

    private static IReadOnlyList<string> BuildContactBusinessBodyLines(ContactMessageRequestDto request) =>
    [
        "A weboldal Kapcsolat űrlapján keresztül új megkeresés érkezett.",
        $"Név: {request.Name}",
        $"Email: {request.Email}",
        $"Telefon: {request.Phone ?? "Nincs megadva"}",
        $"Tárgy: {request.Subject ?? "Nincs megadva"}",
        $"Üzenet: {request.Message}"
    ];

    private static string BuildContactBusinessPlainTextBody(ContactMessageRequestDto request) =>
        $"""
        Új üzenet érkezett a weboldal Kapcsolat űrlapjáról.

        Név: {request.Name}
        Email: {request.Email}
        Telefon: {request.Phone ?? "Nincs megadva"}
        Tárgy: {request.Subject ?? "Nincs megadva"}

        Üzenet:
        {request.Message}
        """;

    private static IReadOnlyList<string> BuildContactVisitorBodyLines(ContactMessageRequestDto request, string submittedDetails) =>
    [
        "Köszönjük, hogy írtál nekünk. Megkaptuk az üzenetedet, és hamarosan válaszolunk a megadott elérhetőségeden.",
        submittedDetails,
        $"Az elküldött üzeneted: {request.Message}"
    ];

    private string BuildContactVisitorPlainTextBody(ContactMessageRequestDto request, string submittedDetails) =>
        $"""
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

    private static IReadOnlyList<string> BuildOrderConfirmationBodyLines(OrderDto order) =>
    [
        "Köszönjük a rendelésedet. A rendelésedet megkaptuk, feldolgozása hamarosan megkezdődik.",
        $"Rendelésszám: {order.OrderNumber}",
        $"Rendelés állapota: {FormatOrderStatus(order.Status)}",
        $"Fizetés állapota: {FormatPaymentStatus(order.PaymentStatus)}",
        $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
        FormatCustomerDetails(order),
        FormatCustomerBillingDocumentDetails(order),
        FormatDeliveryDetails(order),
        FormatPurchaseDetails(order),
        $"Végösszeg: {FormatPrice(order.TotalPrice)}",
        FormatOrderItemsList(order)
    ];

    private static IReadOnlyList<string> BuildOrderBusinessBodyLines(OrderDto order) =>
    [
        "Új rendelés érkezett a weboldalon.",
        "A rendelést az admin felületen tudod feldolgozni.",
        $"Rendelésszám: {order.OrderNumber}",
        FormatCustomerDetails(order),
        FormatAdminBillingDocumentDetails(order),
        FormatDeliveryDetails(order),
        $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
        FormatPurchaseDetails(order),
        $"Végösszeg: {FormatPrice(order.TotalPrice)}",
        FormatOrderItemsList(order)
    ];

    private static IReadOnlyList<string> BuildOrderStatusChangedBodyLines(OrderDto order) =>
    [
        $"A rendelésed állapota megváltozott: {FormatOrderStatus(order.Status)}.",
        $"Rendelésszám: {order.OrderNumber}",
        $"Fizetés állapota: {FormatPaymentStatus(order.PaymentStatus)}",
        $"Átvétel módja: {FormatFulfillmentMethod(order.FulfillmentMethod)}",
        FormatPurchaseDetails(order),
        $"Végösszeg: {FormatPrice(order.TotalPrice)}"
    ];

    private static IReadOnlyList<string> BuildWithdrawalBusinessBodyLines(OrderDto order, OrderWithdrawalRequestDto request) =>
    [
        "Új elállási vagy fogyasztói kérés érkezett.",
        $"Rendelésszám: {order.OrderNumber}",
        $"Vevő: {request.CustomerName}",
        $"Email: {request.CustomerEmail}",
        $"Kérés ideje: {FormatBudapestTime(request.RequestedAtUtc)}",
        $"Üzenet: {request.Reason}"
    ];

    private static IReadOnlyList<string> BuildWithdrawalCustomerBodyLines(OrderDto order, OrderWithdrawalRequestDto request) =>
    [
        $"Megkaptuk a(z) {order.OrderNumber} számú rendeléshez kapcsolódó kérésedet.",
        "A kérésedet rögzítettük, és a feldolgozásáról a megadott elérhetőségen egyeztetünk.",
        $"Megadott üzenet: {request.Reason}"
    ];

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

        lines.Add($"- Cím: {FormatAddress(order.PostalCode, order.City, order.Street, order.HouseNumber, order.Building, order.Floor, order.Door, order.Doorbell)}");

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
            $"- Cím: {FormatAddress(order.PostalCode, order.City, order.Street, order.HouseNumber, order.Building, order.Floor, order.Door, order.Doorbell)}",
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
            $"- Számlázási cím: {FormatAddress(order.PostalCode, order.City, order.Street, order.HouseNumber, order.Building, order.Floor, order.Door, order.Doorbell)}"
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
                order.DeliveryDoorbell
            )}"
        });
    }

    private static string FormatAddress(string? postalCode, string? city, string? street, string? houseNumber, string? building, string? floor, string? door, string? doorbell)
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

        return string.Join("; ", order.Items.Select(item => $"{item.ProductName} - {item.Quantity} {FormatUnit(item.Unit)}, {FormatPrice(item.LineTotal)}"));
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
        FulfillmentMethod.ParcelShop => "GLS CsomagPont",
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
