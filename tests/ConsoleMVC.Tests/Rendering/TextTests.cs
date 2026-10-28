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
    [InlineData("\u2764\uFE0F", 2)]  // heart with emoji presentation (❤️)
    [InlineData("\u2764", 1)]         // heart as a text symbol
    [InlineData("👍🏽", 2)]          // skin tone modifier
    [InlineData("👨\u200D👩\u200D👧", 2)] // family: one ZWJ sequence
    [InlineData("🇬🇧", 2)]          // flag: a regional indicator pair
    [InlineData("a👍🏽b", 4)]
    public void TextWidth_GetWidth_CountsTerminalCells(string text, int expected)
    {
        Assert.Equal(expected, TextWidth.Of(text));
    }

    [Theory]
    [InlineData("Hello world", 5, "Hell…")]
    [InlineData("Hello", 5, "Hello")]
    [InlineData("Hello", 1, "…")]
    [InlineData("日本語", 5, "日本…")]
    [InlineData("👨\u200D👩\u200D👧xyz", 3, "👨\u200D👩\u200D👧…")]
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

        Assert.Equal("Bob", TextSanitiser.Sanitise(hostile));
    }

    [Theory]
    [InlineData("a\u001b]0;Owned\u0007b", "ab")]             // a window title, ended by BEL
    [InlineData("a\u001b]8;;https://x\u001b\\link\u001b]8;;\u001b\\b", "alinkb")]   // a hyperlink, ended by ESC \
    [InlineData("a\u001bcb", "ab")]                           // ESC c resets the terminal
    [InlineData("a\u001b(Bb", "ab")]                          // a character set choice: an intermediate and a final
    [InlineData("a\u001b[?1049hb", "ab")]                     // the alternate screen
    [InlineData("a\u001b[", "a")]                             // cut short at the end
    [InlineData("a\u001b[12", "a12")]                         // unfinished: the rest is kept as text
    [InlineData("a\u001b]0;never ended", "a0;never ended")]    // unterminated: only its introducer goes, not the text
    public void TextSanitiser_RemovesWholeSequences_AndKeepsTheTextAfterThem(string input, string expected)
    {
        Assert.Equal(expected, TextSanitiser.Sanitise(input));
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
        Assert.Equal("ab", TextSanitiser.Sanitise("a\u0085b"));          // NEL, a C1 control
        Assert.Equal("a", TextSanitiser.Sanitise("a\u009bb"));           // the C1 CSI introducer starts a sequence: 'b' ends it
        Assert.Equal("ab", TextSanitiser.Sanitise("a\u009b31mb"));
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
