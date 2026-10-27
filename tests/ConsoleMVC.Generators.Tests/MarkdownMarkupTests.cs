using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for <c>&lt;markdown /&gt;</c> (docs/design/markdown.md).</summary>
public class MarkdownMarkupTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    [Fact]
    public void Markdown_DrawsTheModel()
    {
        var output = ViewHarness.Render(
            "@model string\n<markdown text=\"@Model\" links=\"false\" heading-colour=\"cyan\" code-color=\"green\" />" + Quit,
            "\"# Help\\n\\n- Press **F1**\\n- See [docs](https://x.org)\"");

        Assert.Equal("Help\n• Press F1\n• See docs\n", output);
    }

    [Fact]
    public void Markdown_TakesALiteral_AndInterpolation()
    {
        Assert.Equal("Hello Ada\n", ViewHarness.Render("@model string\n<markdown text=\"**Hello** @Model\" />" + Quit, "\"Ada\""));
    }

    [Theory]
    [InlineData("<markdown />")]
    [InlineData("<markdown text=\"x\">content</markdown>")]
    [InlineData("<markdown text=\"x\" links=\"maybe\" />")]
    public void Markdown_RejectsInvalidMarkup(string markup)
    {
        Assert.NotEmpty(ViewHarness.Generate(markup + Quit).GeneratorDiagnostics);
    }
}
