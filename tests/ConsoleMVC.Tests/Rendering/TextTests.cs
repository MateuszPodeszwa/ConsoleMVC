using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for the text utilities: <see cref="TextWidth"/>, <see cref="TextSanitiser"/> and <see cref="TextWrapper"/>.
/// </summary>
public class TextTests
{
    // --- TextWidth ---

    [Theory]
    [InlineData("", 0)]
    [InlineData("Hello", 5)]
    [InlineData("日本", 4)]          // East Asian wide characters occupy two cells
    [InlineData("é", 1)]      // combining acute accent occupies no cell
    [InlineData("🙂", 2)]           // emoji outside the BMP
    public void TextWidth_GetWidth_CountsTerminalCells(string text, int expected)
    {
        Assert.Equal(expected, TextWidth.Of(text));
    }

    [Theory]
    [InlineData("Hello world", 5, "Hell…")]
    [InlineData("Hello", 5, "Hello")]
    [InlineData("Hello", 1, "…")]
    [InlineData("日本語", 5, "日本…")]
    public void TextWidth_Truncate_AddsEllipsis(string text, int width, string expected)
    {
        Assert.Equal(expected, TextWidth.Truncate(text, width));
    }

    // --- TextSanitiser ---

    [Fact]
    public void TextSanitiser_RemovesEscapeSequences()
    {
        // A malicious model value trying to clear the screen and recolour the terminal
        var hostile = "Bob\u001b[2J\u001b[31m";

        Assert.Equal("Bob[2J[31m", TextSanitiser.Sanitise(hostile));
    }

    [Fact]
    public void TextSanitiser_KeepsNewlines_NormalisesCarriageReturns()
    {
        Assert.Equal("a\nb\nc", TextSanitiser.Sanitise("a\r\nb\rc"));
    }

    [Fact]
    public void TextSanitiser_ExpandsTabs()
    {
        Assert.Equal("a    b", TextSanitiser.Sanitise("a\tb"));
    }

    [Fact]
    public void TextSanitiser_RemovesBidirectionalOverrides()
    {
        Assert.Equal("abc", TextSanitiser.Sanitise("a‮b⁦c"));
    }

    [Fact]
    public void TextSanitiser_RemovesC1Controls()
    {
        Assert.Equal("ab", TextSanitiser.Sanitise("a\u009bb"));
    }

    // --- TextWrapper ---

    [Fact]
    public void TextWrapper_Wrap_BreaksAtWordBoundaries()
    {
        var lines = TextWrapper.Wrap("The quick brown fox jumps", 10);

        Assert.Equal(["The quick", "brown fox", "jumps"], lines);
    }

    [Fact]
    public void TextWrapper_Wrap_BreaksWordsLongerThanWidth()
    {
        var lines = TextWrapper.Wrap("abcdefghij kl", 4);

        Assert.Equal(["abcd", "efgh", "ij", "kl"], lines);
    }

    [Fact]
    public void TextWrapper_Wrap_PreservesHardLineBreaksAndIndentation()
    {
        var lines = TextWrapper.Wrap("  [1] One\n  [2] Two", 20);

        Assert.Equal(["  [1] One", "  [2] Two"], lines);
    }

    [Fact]
    public void TextWrapper_Wrap_EmptyTextProducesOneEmptyLine()
    {
        Assert.Equal([""], TextWrapper.Wrap("", 10));
    }

    [Fact]
    public void TextWrapper_Truncate_KeepsOneLinePerParagraph()
    {
        var lines = TextWrapper.Truncate("A very long line\nshort", 8);

        Assert.Equal(["A very …", "short"], lines);
    }
}
