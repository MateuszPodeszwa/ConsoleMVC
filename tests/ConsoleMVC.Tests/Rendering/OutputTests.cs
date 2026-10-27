using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for the render targets and the <see cref="MarkupRenderer"/> facade.
/// </summary>
public class OutputTests
{
    private const string Esc = "\u001b";

    private static RenderedLine Line(string text, TextStyle style) =>
        new([new Span(text, style)]);

    // --- AnsiRenderTarget ---

    [Fact]
    public void Ansi_PlainStyle_WritesNoEscapeCodes()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new AnsiRenderTarget(writer, ColourSupport.TrueColour);

        target.Write(Line("hello", TextStyle.Plain));

        Assert.Equal("hello\n", writer.ToString());
    }

    [Fact]
    public void Ansi_ConsoleColourAndBold_WritesSgrAndReset()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new AnsiRenderTarget(writer, ColourSupport.Basic);
        var style = TextStyle.Plain.WithForeground(Colour.Red).WithDecoration(TextDecoration.Bold);

        target.Write(Line("x", style));

        Assert.Equal($"{Esc}[1;91mx{Esc}[0m\n", writer.ToString());
    }

    [Fact]
    public void Ansi_DarkColourAndBackground_UseStandardCodes()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new AnsiRenderTarget(writer, ColourSupport.Basic);
        var style = TextStyle.Plain.WithForeground(Colour.DarkRed).WithBackground(Colour.Blue);

        target.Write(Line("x", style));

        Assert.Equal($"{Esc}[31;104mx{Esc}[0m\n", writer.ToString());
    }

    [Fact]
    public void Ansi_RgbColour_TrueColourSupport_WritesTwentyFourBitCode()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new AnsiRenderTarget(writer, ColourSupport.TrueColour);

        target.Write(Line("x", TextStyle.Plain.WithForeground(Colour.FromRgb(255, 136, 0))));

        Assert.Equal($"{Esc}[38;2;255;136;0mx{Esc}[0m\n", writer.ToString());
    }

    [Fact]
    public void Ansi_RgbColour_BasicSupport_FallsBackToNearestConsoleColour()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new AnsiRenderTarget(writer, ColourSupport.Basic);

        target.Write(Line("x", TextStyle.Plain.WithForeground(Colour.FromRgb(250, 10, 10))));

        Assert.Equal($"{Esc}[91mx{Esc}[0m\n", writer.ToString());
    }

    [Fact]
    public void Ansi_NoColourSupport_KeepsDecorationsOnly()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new AnsiRenderTarget(writer, ColourSupport.None);
        var style = TextStyle.Plain.WithForeground(Colour.Red).WithDecoration(TextDecoration.Underline);

        target.Write(Line("x", style));

        Assert.Equal($"{Esc}[4mx{Esc}[0m\n", writer.ToString());
    }

    // --- PlainTextRenderTarget ---

    [Fact]
    public void Plain_WritesTextOnly_AndIgnoresClear()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var target = new PlainTextRenderTarget(writer);

        target.Write(Line("x", TextStyle.Plain.WithForeground(Colour.Red)));
        target.Clear();

        Assert.Equal("x\n", writer.ToString());
    }

    // --- MarkupRenderer ---

    [Fact]
    public void Renderer_LaysOutElementUsingConfiguredWidth()
    {
        var writer = new StringWriter { NewLine = "\n" };
        var renderer = new MarkupRenderer(new PlainTextRenderTarget(writer), () => 12);
        var text = new TextElement { Align = Alignment.Right };
        text.AppendText("ab");

        renderer.Render(text);

        Assert.Equal(12, renderer.Width);
        Assert.Equal("          ab\n", writer.ToString());
    }

    [Fact]
    public void Renderer_EnforcesMinimumWidth()
    {
        var renderer = new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => -5);

        Assert.Equal(MarkupRenderer.MinimumWidth, renderer.Width);
    }
}

/// <summary>
/// Tests for environment-based colour detection in <see cref="TerminalCapabilities"/>.
/// </summary>
public class TerminalCapabilitiesTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [Fact]
    public void NoColor_DisablesColour() =>
        Assert.Equal(ColourSupport.None, TerminalCapabilities.DetectColourSupport(Env(("NO_COLOR", "1"), ("COLORTERM", "truecolor"))));

    [Fact]
    public void DumbTerminal_DisablesColour() =>
        Assert.Equal(ColourSupport.None, TerminalCapabilities.DetectColourSupport(Env(("TERM", "dumb"))));

    [Theory]
    [InlineData("COLORTERM", "truecolor")]
    [InlineData("COLORTERM", "24bit")]
    [InlineData("WT_SESSION", "abc")]
    public void TrueColourTerminals_AreDetected(string key, string value) =>
        Assert.Equal(ColourSupport.TrueColour, TerminalCapabilities.DetectColourSupport(Env((key, value))));

    [Fact]
    public void Default_IsBasic() =>
        Assert.Equal(ColourSupport.Basic, TerminalCapabilities.DetectColourSupport(Env()));
}
