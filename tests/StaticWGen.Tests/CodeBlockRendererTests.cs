namespace StaticWGen.Tests;

public class CodeBlockRendererTests
{
    [Fact]
    public void MermaidBlock_RendersWithMermaidClass()
    {
        var md = "```mermaid\ngraph TD;\n    A-->B;\n```";
        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(md);

        Assert.Contains("<pre class=\"mermaid\">", html);
        Assert.DoesNotContain("language-mermaid", html);
        Assert.Contains("A--&gt;B;", html);
    }

    [Fact]
    public void CSharpBlock_RendersWithLanguageClass()
    {
        var md = "```csharp\nConsole.WriteLine(\"Hello\");\n```";
        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(md);

        Assert.Contains("class=\"language-csharp\"", html);
        Assert.Contains("Console.WriteLine", html);
    }

    [Fact]
    public void PythonBlock_RendersWithLanguageClass()
    {
        var md = "```python\nprint('hello')\n```";
        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(md);

        Assert.Contains("class=\"language-python\"", html);
    }

    [Fact]
    public void MermaidAndCodeBlocks_CoexistCorrectly()
    {
        var md = """
            ```csharp
            var x = 1;
            ```

            ```mermaid
            graph TD;
                A-->B;
            ```

            ```javascript
            const y = 2;
            ```
            """;
        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(md);

        Assert.Contains("class=\"language-csharp\"", html);
        Assert.Contains("<pre class=\"mermaid\">", html);
        Assert.Contains("class=\"language-javascript\"", html);
        Assert.DoesNotContain("language-mermaid", html);
    }

    [Fact]
    public void IndentedCodeBlock_RendersWithoutLanguageClass()
    {
        var md = "    var x = 1;";
        var html = MarkdownPipelineHelper.ConvertToHtmlWithMermaidRenderer(md);

        Assert.Contains("<pre><code>", html);
    }
}
