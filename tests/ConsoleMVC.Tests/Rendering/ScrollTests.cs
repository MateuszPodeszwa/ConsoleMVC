using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>Tests for <c>&lt;scroll&gt;</c> and full-screen output (docs/design/full-screen.md).</summary>
public class ScrollTests
{
    private static ScrollElement Numbers(int count, int? height = 3)
    {
        var scroll = new ScrollElement { Height = height };
        for (var i = 1; i <= count; i++)
        {
            var text = new TextElement();
            text.AppendText($"Line {i}");
            scroll.Add(text);
        }

        return scroll;
    }

    private static (MarkupRenderer Renderer, RewritableScreen Screen) Terminal(int height = 24)
    {
        var screen = new RewritableScreen();
        var renderer = new MarkupRenderer(screen, () => 40, () => height);
        renderer.Clear();
        return (renderer, screen);
    }

    private static string[] Visible(RewritableScreen screen) => screen.Screen.TrimEnd('\n').Split('\n');

    [Fact]
    public void Scroll_ShowsOnePage_AndMovesWithTheKeys()
    {
        var (renderer, screen) = Terminal();
        var keys = new ScriptedKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);
        var pages = new List<string[]>();
        var scroll = Numbers(10);

        scroll.Show(renderer, keys, onPage: () => pages.Add(Visible(screen)));

        Assert.Equal(["Line 1", "Line 2", "Line 3"], pages[0][..3]);
        Assert.Equal("Lines 1-3 of 10", pages[0][3][..15]);
        Assert.Equal(["Line 3", "Line 4", "Line 5"], pages[2][..3]);
        Assert.Equal(["Line 3", "Line 4", "Line 5"], Visible(screen)); // the status line goes once the user continues
    }

    [Theory]
    [InlineData(ConsoleKey.PageDown, "Line 4")]
    [InlineData(ConsoleKey.Spacebar, "Line 4")]
    [InlineData(ConsoleKey.End, "Line 8")]
    [InlineData(ConsoleKey.UpArrow, "Line 1")]
    [InlineData(ConsoleKey.Home, "Line 1")]
    public void Scroll_PagesAndJumps_WithinTheContent(ConsoleKey key, string firstLine)
    {
        var (renderer, screen) = Terminal();

        Numbers(10).Show(renderer, new ScriptedKeys(key, ConsoleKey.Enter));

        Assert.Equal(firstLine, Visible(screen)[0]);
    }

    [Fact]
    public void Scroll_EndsWithEscapeOrQ_AndAtTheEndOfInput()
    {
        foreach (var keys in new[] { new ScriptedKeys(ConsoleKey.Escape), new ScriptedKeys(ConsoleKey.Q), new ScriptedKeys(Array.Empty<ConsoleKey>()) })
        {
            var (renderer, screen) = Terminal();
            Numbers(10).Show(renderer, keys);
            Assert.Equal(["Line 1", "Line 2", "Line 3"], Visible(screen));
        }
    }

    [Fact]
    public void Scroll_DrawsEverything_WhenItFits_OrKeysCannotBeRead()
    {
        var (renderer, screen) = Terminal();
        Numbers(3).Show(renderer, new ScriptedKeys(ConsoleKey.DownArrow));
        Assert.Equal(["Line 1", "Line 2", "Line 3"], Visible(screen));

        var output = new StringWriter { NewLine = "\n" };
        var piped = new MarkupRenderer(new PlainTextRenderTarget(output), () => 40);
        Numbers(5).Show(piped, new ScriptedInput("ignored"));
        Assert.Equal("Line 1\nLine 2\nLine 3\nLine 4\nLine 5\n", output.ToString());
    }

    [Fact]
    public void Scroll_FitsItsPagesToTheWindow_WithoutAHeight()
    {
        var (renderer, screen) = Terminal(height: 8); // 8 rows: 5 lines, the status line and room for the prompt

        Numbers(20, height: null).Show(renderer, new ScriptedKeys(ConsoleKey.Enter));

        Assert.Equal(5, Visible(screen).Length);
    }

    [Fact]
    public void Scroll_AsAnElement_LaysOutAllItsContent()
    {
        Assert.Equal(10, Numbers(10).Layout(40).Count);
    }

    // ------------------------------------------------------------------ full screen

    [Fact]
    public void FullScreen_SwitchesTheAlternateBuffer_OnAnsiTerminals()
    {
        var output = new StringWriter();
        var renderer = new MarkupRenderer(new AnsiRenderTarget(output, ColourSupport.Basic), () => 40);

        Assert.True(renderer.SetFullScreen(true));
        Assert.True(renderer.SetFullScreen(false));

        Assert.Equal("\u001b[?1049h\u001b[H\u001b[?1049l", output.ToString());
    }

    [Fact]
    public void FullScreen_IsRefused_WhereThereIsNoScreen()
    {
        var renderer = new MarkupRenderer(new PlainTextRenderTarget(new StringWriter()), () => 40);

        Assert.False(renderer.SetFullScreen(true));
    }
}
