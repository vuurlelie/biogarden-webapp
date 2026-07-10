using System.Net;
using Biogarden.BusinessLogic.Abstractions;
using Biogarden.BusinessLogic.Models.Email;
using Biogarden.BusinessLogic.Options;
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
            greeting: "Kedves Vásárlónk!",
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
            greeting: "Kedves Vásárlónk!",
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
                <img src="{HtmlAttributeEncode(_brandingOptions.LogoUrl)}" width="54" height="54" alt="{HtmlAttributeEncode(_brandingOptions.CompanyName)}" style="display:block;border:0;outline:none;text-decoration:none;max-width:54px;height:auto;">
                """;

        return $$"""
            <table role="presentation" cellspacing="0" cellpadding="0" width="100%">
              <tr>
                <td width="66" valign="top">{{logoMarkup}}</td>
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
}
