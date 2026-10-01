using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace Biogarden.BusinessLogic.Validation;

/// <summary>The HTML subset supported by the article editor and public readers.</summary>
public static class ArticleHtml
{
    public const int MaximumLength = 100_000;
    public const int MinimumTextLength = 20;

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "p", "br", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "em", "u", "s",
            "ul", "ol", "li", "blockquote", "pre", "code", "hr", "span", "a"
        ]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title", "style", "start"]);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.UnionWith(["color", "font-family", "font-size", "text-align"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["https", "http", "mailto"]);
        sanitizer.UriAttributes.Clear();
        sanitizer.UriAttributes.Add("href");
        return sanitizer.Sanitize(html).Trim();
    }

    public static string ValidateAndSanitize(string? html)
    {
        // Bound parser work before processing an untrusted request.
        if (html?.Length > MaximumLength)
            throw new ArgumentException($"A bejegyzés HTML szövege legfeljebb {MaximumLength} karakter lehet.");

        var sanitized = Sanitize(html);
        using var document = new HtmlParser().ParseDocument(sanitized);
        var text = document.Body?.TextContent ?? string.Empty;
        if (text.Count(character => !char.IsWhiteSpace(character) &&
                char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.Format) < MinimumTextLength)
            throw new ArgumentException("A bejegyzés szövege legalább 20 látható karakter legyen.");

        return sanitized;
    }
}
