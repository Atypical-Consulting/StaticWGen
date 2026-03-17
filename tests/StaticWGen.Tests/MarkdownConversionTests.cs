namespace StaticWGen.Tests;

public class MarkdownConversionTests
{
    [Fact]
    public void ConvertToHtml_Heading_RendersH1()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("# Hello World");
        Assert.Contains("<h1", html);
        Assert.Contains("Hello World", html);
    }

    [Fact]
    public void ConvertToHtml_Paragraph_RendersP()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("This is a paragraph.");
        Assert.Contains("<p>This is a paragraph.</p>", html);
    }

    [Fact]
    public void ConvertToHtml_UnorderedList_RendersUl()
    {
        var md = "- Item 1\n- Item 2\n- Item 3";
        var html = MarkdownPipelineHelper.ConvertToHtml(md);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>Item 1</li>", html);
        Assert.Contains("<li>Item 2</li>", html);
    }

    [Fact]
    public void ConvertToHtml_Link_RendersAnchor()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("[Click here](https://example.com)");
        Assert.Contains("<a href=\"https://example.com\"", html);
        Assert.Contains("Click here</a>", html);
    }

    [Fact]
    public void ConvertToHtml_Image_RendersImg()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("![Alt text](image.png)");
        Assert.Contains("<img src=\"image.png\"", html);
        Assert.Contains("alt=\"Alt text\"", html);
    }

    [Fact]
    public void ConvertToHtml_Table_RendersTable()
    {
        var md = "| A | B |\n|---|---|\n| 1 | 2 |";
        var html = MarkdownPipelineHelper.ConvertToHtml(md);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>A</th>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void ConvertToHtml_Emoji_RendersUnicodeEmoji()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("Hello :wave:");
        Assert.Contains("👋", html);
    }

    [Fact]
    public void ConvertToHtml_MultipleHeadingLevels_RendersCorrectly()
    {
        var md = "# H1\n## H2\n### H3";
        var html = MarkdownPipelineHelper.ConvertToHtml(md);
        Assert.Contains("<h1", html);
        Assert.Contains("<h2", html);
        Assert.Contains("<h3", html);
    }

    [Fact]
    public void ConvertToHtml_Bold_RendersStrong()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("**bold text**");
        Assert.Contains("<strong>bold text</strong>", html);
    }

    [Fact]
    public void ConvertToHtml_Italic_RendersEm()
    {
        var html = MarkdownPipelineHelper.ConvertToHtml("*italic text*");
        Assert.Contains("<em>italic text</em>", html);
    }
}
