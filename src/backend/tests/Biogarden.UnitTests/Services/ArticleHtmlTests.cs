using Biogarden.BusinessLogic.DTOs.Posts;
using Biogarden.BusinessLogic.Mappers;
using Biogarden.BusinessLogic.Validation;
using Biogarden.DataAccess.Entities;

namespace Biogarden.UnitTests.Services;

public sealed class ArticleHtmlTests
{
    [Theory]
    [InlineData("<script>alert(1)</script><p>Safe content</p>")]
    [InlineData("<img src=x onerror=alert(1)><p>Safe content</p>")]
    [InlineData("<iframe srcdoc='<script>alert(1)</script>'></iframe><p>Safe content</p>")]
    [InlineData("<svg onload=alert(1)></svg><p>Safe content</p>")]
    [InlineData("<math><mtext><img src=x onerror=alert(1)></mtext></math><p>Safe content</p>")]
    public void Sanitize_RemovesExecutableElements(string html)
    {
        Assert.Equal("<p>Safe content</p>", ArticleHtml.Sanitize(html));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("jav&#x61;script:alert(1)")]
    [InlineData("java&#x09;script:alert(1)")]
    [InlineData("data:text/html,attack")]
    [InlineData("vbscript:attack")]
    public void Sanitize_RemovesUnsafeLinks(string href)
    {
        Assert.Equal("<a>Read more</a>", ArticleHtml.Sanitize($"<a href='{href}' onclick='attack()' target='_blank'>Read more</a>"));
    }

    [Fact]
    public void Sanitize_PreservesEditorFormattingAndSafeLinks()
    {
        const string html = "<h2>Heading</h2><p style=\"text-align: center\"><span style=\"color: rgb(10, 20, 30); font-family: Georgia; font-size: 1.125rem\"><strong>Bold</strong><em>Italic</em><u>Underline</u><s>Strike</s></span><br></p><ol start=\"3\"><li>List</li></ol><blockquote>Quote</blockquote><pre><code>Code</code></pre><hr><a href=\"https://example.com\" title=\"Link\">Link</a>";
        Assert.Equal(html.Replace("rgb(10, 20, 30)", "rgba(10, 20, 30, 1)"), ArticleHtml.Sanitize(html));
    }

    [Fact]
    public void Sanitize_RemovesLayoutAndUrlStyles()
    {
        var result = ArticleHtml.Sanitize("<p id='x' class='overlay' style='position:fixed;background:url(https://example.com);color:red' onmouseover='attack()'>Text</p>");
        Assert.Equal("<p style=\"color: rgba(255, 0, 0, 1)\">Text</p>", result);
    }

    [Theory]
    [InlineData("<p></p><p></p><p></p><p></p>")]
    [InlineData("<script>Enough characters hidden inside a script tag</script>")]
    [InlineData("<p>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</p>")]
    public void ValidateAndSanitize_RejectsMarkupWithoutEnoughVisibleText(string html)
        => Assert.Throws<ArgumentException>(() => ArticleHtml.ValidateAndSanitize(html));

    [Fact]
    public void ValidateAndSanitize_BoundsInputBeforeParsing()
        => Assert.Throws<ArgumentException>(() => ArticleHtml.ValidateAndSanitize(new string('x', ArticleHtml.MaximumLength + 1)));

    [Fact]
    public void Mappings_SanitizeWritesAndLegacyReadsWithoutMutatingStoredLegacyContent()
    {
        const string html = "<p>Fresh vegetables from our garden.</p><script>alert(1)</script>";
        const string safe = "<p>Fresh vegetables from our garden.</p>";
        var created = new PostCreateDto { Title = "Article", Article = html }.ToEntity();
        Assert.Equal(safe, created.Article);
        new PostEditDto { Title = "Updated", Article = html }.ApplyChanges(created);
        Assert.Equal(safe, created.Article);

        var legacy = new Post { Title = "Legacy", Article = html };
        Assert.Equal(safe, legacy.ToDetailsDto().Article);
        Assert.Equal(safe, legacy.ToListItemDto().Article);
        Assert.Equal(safe, Assert.Single(new[] { legacy }.ToListItemDtos()).Article);
        Assert.Equal(html, legacy.Article);
    }
}
