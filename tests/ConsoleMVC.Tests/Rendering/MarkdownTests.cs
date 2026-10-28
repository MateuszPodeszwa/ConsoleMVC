using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>Tests for <c>&lt;markdown&gt;</c> (docs/design/markdown.md).</summary>
public class MarkdownTests
{
    private static string Plain(string markdown, int width = 40, Action<MarkdownElement>? configure = null)
    {
        var element = new MarkdownElement { Text = markdown };
        configure?.Invoke(element);
        return string.Join('\n', element.Layout(width).Select(l => l.ToPlainText()));
    }

    private static IReadOnlyList<RenderedLine> Lines(string markdown, int width = 40) =>
        new MarkdownElement { Text = markdown }.Layout(width);

    private static TextStyle StyleOf(IReadOnlyList<RenderedLine> lines, string text) =>
        lines.SelectMany(l => l.Spans).First(s => s.Text.Contains(text)).Style;

    // ------------------------------------------------------------------ blocks

    [Fact]
    public void Blocks_AreSeparatedByBlankLines_ExceptAfterHeadings()
    {
        var output = Plain("# Release notes\n\nThis release adds\ncharts and more.\n\n## Fixes\nNone.");

        // No blank line after a heading: it belongs to what follows
        Assert.Equal("Release notes\nThis release adds charts and more.\n\nFixes\nNone.", output);
    }

    [Fact]
    public void Headings_AreBold_AndTheFirstLevelIsUnderlined()
    {
        var lines = Lines("# One\n## Two\n### Three");

        Assert.Equal(TextDecoration.Bold | TextDecoration.Underline, StyleOf(lines, "One").Decoration);
        Assert.Equal(TextDecoration.Bold, StyleOf(lines, "Two").Decoration);
        Assert.Equal(TextDecoration.Bold, StyleOf(lines, "Three").Decoration);
    }

    [Fact]
    public void Paragraphs_WrapToTheWidth()
    {
        var output = Plain("The quick brown fox jumps over the lazy dog", 16);

        Assert.Equal("The quick brown\nfox jumps over\nthe lazy dog", output);
    }

    [Fact]
    public void Lists_UseBullets_Numbers_NestingAndHangingIndents()
    {
        var output = Plain("- first item that wraps\n- second\n  - nested\n\n1. one\n2. two", 16);

        Assert.Equal("• first item\n  that wraps\n• second\n  • nested\n\n1. one\n2. two", output);
    }

    [Fact]
    public void CodeBlocks_KeepTheirLines_AndAreNotFormatted()
    {
        var output = Plain("Run:\n\n```bash\ndotnet run   --project *App*\n```\nDone.");

        Assert.Equal("Run:\n\n  dotnet run   --project *App*\n\nDone.", output);
    }

    [Fact]
    public void CodeBlocks_AreClipped_NotWrapped()
    {
        var output = Plain("```\nabcdefghijklmnop\n```", 10);

        Assert.Equal("  abcdefgh", output);
    }

    [Fact]
    public void Quotes_AreMarked_AndRulesFillTheWidth()
    {
        var output = Plain("> Quoted text\n> continues\n\n---\nAfter", 20);

        Assert.Equal("│ Quoted text\n│ continues\n\n" + new string('─', 20) + "\n\nAfter", output);
    }

    // ------------------------------------------------------------------ inline

    [Fact]
    public void Inline_BoldItalicAndCode_AreStyled_AndTheMarkersRemoved()
    {
        var lines = Lines("Use **bold**, *italic*, _also_ and `code` here");

        Assert.Equal("Use bold, italic, also and code here", lines[0].ToPlainText());
        Assert.Equal(TextDecoration.Bold, StyleOf(lines, "bold").Decoration);
        Assert.Equal(TextDecoration.Italic, StyleOf(lines, "italic").Decoration);
        Assert.Equal(TextDecoration.Italic, StyleOf(lines, "also").Decoration);
        Assert.Equal(MarkdownElement.DefaultCodeColour, StyleOf(lines, "code").Foreground);
    }

    [Fact]
    public void Inline_NestsEmphasis_AndKeepsWhatIsNotFormatting()
    {
        var lines = Lines("**bold *both***, snake_case_name, 2 * 3 * 4, `**raw**`, \\*escaped\\*, **unclosed", 200);

        Assert.Equal("bold both, snake_case_name, 2 * 3 * 4, **raw**, *escaped*, **unclosed", lines[0].ToPlainText());
        Assert.Equal(TextDecoration.Bold | TextDecoration.Italic, StyleOf(lines, "both").Decoration);
    }

    [Fact]
    public void Links_ShowTheirTextAndAddress()
    {
        Assert.Equal("See the guide (https://example.com/guide).", Plain("See [the guide](https://example.com/guide).", 60));
        Assert.Equal("See the guide.", Plain("See [the guide](https://example.com/guide).", 60, m => m.ShowLinkUrls = false));
        Assert.Equal("Mail https://example.com", Plain("Mail <https://example.com>", 60));

        var lines = Lines("[here](https://x.org)", 60);
        Assert.True(StyleOf(lines, "here").Decoration.HasFlag(TextDecoration.Underline));
    }

    [Fact]
    public void HardLineBreaks_EndALine()
    {
        Assert.Equal("one\ntwo", Plain("one  \ntwo"));
        Assert.Equal("one\ntwo", Plain("one\\\ntwo"));
    }

    // ------------------------------------------------------------------ safety

    [Fact]
    public void Markdown_CannotInjectTerminalSequences()
    {
        var output = Plain("# Title\u001b[2J\n\n[click\u001b]8;;evil\u0007](https://x\u001b[31m.org)\n\n```\n\u001b[31mred\n```");

        Assert.DoesNotContain('\u001b', output);
        Assert.DoesNotContain('\u0007', output);
    }

    [Fact]
    public void Markdown_StaysFast_WithManyUnclosedDelimiters()
    {
        var text = string.Concat(Enumerable.Repeat("**a *b _c [d `e ", 2000)) + "*";
        var timer = System.Diagnostics.Stopwatch.StartNew();

        var lines = Lines(text, 80);
        var closedAtTheEnd = Lines(string.Concat(Enumerable.Repeat("*x _y **z ", 3000)) + "w* w_ w**", 80);

        Assert.NotEmpty(lines);
        Assert.NotEmpty(closedAtTheEnd);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"took {timer.Elapsed}");
    }

    [Fact]
    public void EmptyText_DrawsNothing()
    {
        Assert.Empty(Lines(""));
        Assert.Empty(Lines("\n\n  \n"));
        Assert.Empty(new MarkdownElement().Layout(40));
    }

    [Fact]
    public void Markdown_FillsItsContainer()
    {
        Assert.Equal(0, new MarkdownElement { Text = "Hello" }.MeasureWidth(40));
    }

    [Theory]
    [InlineData("See ![a chart](chart.png) here", "See [image: a chart] here")]
    [InlineData("![](x.png)", "[image]")]
    [InlineData("Not an image: ! [x](y)", "Not an image: ! x (y)")]
    public void Images_AreDescribed_InsteadOfDrawnAsLinks(string markdown, string expected)
    {
        Assert.Equal(expected, Plain(markdown, width: 60).TrimEnd());
    }
}
