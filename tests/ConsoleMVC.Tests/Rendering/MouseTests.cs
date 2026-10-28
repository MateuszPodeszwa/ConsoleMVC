using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>Tests for mouse support: decoding clicks and clicking menus and choices (docs/design/mouse.md).</summary>
public class MouseTests
{
    /// <summary>The keys a terminal sends for a mouse event in SGR mode: ESC [ &lt; button ; column ; row M.</summary>
    private static ConsoleKeyInfo[] Sgr(int button, int column, int row, bool press = true) =>
        $"\u001b[<{button};{column + 1};{row + 1}{(press ? 'M' : 'm')}".Select(Char).ToArray();

    private static ConsoleKeyInfo Char(char c) =>
        c == '\u001b' ? ScriptedKeys.Key(ConsoleKey.Escape) : new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false);

    private static ConsoleKeyInfo[] Click(int column, int row) => [..Sgr(0, column, row), ..Sgr(0, column, row, press: false)];

    private static (MarkupRenderer Renderer, RewritableScreen Screen) Terminal()
    {
        var screen = new RewritableScreen();
        var renderer = new MarkupRenderer(screen, () => 40, () => 24);
        renderer.Clear();
        return (renderer, screen);
    }

    private static TextElement Text(string text)
    {
        var element = new TextElement();
        element.AppendText(text);
        return element;
    }

    // ------------------------------------------------------------------ decoding

    [Fact]
    public void Clicks_OnAMappedLine_BecomeItsKeys()
    {
        var (renderer, _) = Terminal();
        renderer.Render(Text("first"));
        renderer.Render(Text("second"));
        renderer.MapClicks([new ClickTarget(0, [ScriptedKeys.Key(ConsoleKey.Spacebar)])]); // line 0 of "second": screen row 1

        var input = new MouseDecoder(new ScriptedKeys([..Click(3, 0), ..Click(3, 1), ScriptedKeys.Key(ConsoleKey.Enter)]), renderer);

        Assert.Equal(ConsoleKey.Spacebar, input.ReadKey()!.Value.Key); // the click on row 0 maps to nothing and is skipped
        Assert.Equal(ConsoleKey.Enter, input.ReadKey()!.Value.Key);
    }

    [Fact]
    public void TheWheel_ScrollsWithTheArrowKeys()
    {
        var (renderer, _) = Terminal();
        var input = new MouseDecoder(new ScriptedKeys([..Sgr(64, 1, 1), ..Sgr(65, 1, 1)]), renderer);

        Assert.Equal(ConsoleKey.UpArrow, input.ReadKey()!.Value.Key);
        Assert.Equal(ConsoleKey.DownArrow, input.ReadKey()!.Value.Key);
    }

    [Fact]
    public void Escape_AndOtherSequences_PassThrough()
    {
        var (renderer, _) = Terminal();
        var input = new MouseDecoder(new ScriptedKeys([ScriptedKeys.Key(ConsoleKey.Escape), Char('x'), Char('\u001b'), Char('['), Char('A')]), renderer);

        Assert.Equal(ConsoleKey.Escape, input.ReadKey()!.Value.Key);
        Assert.Equal('x', input.ReadKey()!.Value.KeyChar);
        Assert.Equal(ConsoleKey.Escape, input.ReadKey()!.Value.Key);
        Assert.Equal('[', input.ReadKey()!.Value.KeyChar);
        Assert.Equal('A', input.ReadKey()!.Value.KeyChar);
        Assert.Null(input.ReadKey());
    }

    [Fact]
    public void BrokenSequences_AreKeptAsTyped()
    {
        var (renderer, _) = Terminal();
        var keys = "\u001b[<0;x".Select(Char).ToArray();

        var input = new MouseDecoder(new ScriptedKeys(keys), renderer);
        var read = new List<char>();
        while (input.ReadKey() is { } key)
            read.Add(key.KeyChar);

        Assert.Equal("\u001b[<0;x", new string(read.ToArray()));
    }

    [Fact]
    public void Rows_FollowWhatWasDrawn_AndClearForgetsTheTargets()
    {
        var (renderer, _) = Terminal();
        renderer.Render(Text("a"));
        renderer.WritePrompt("Name: ", TextStyle.Plain);
        renderer.RecordInput("Ada");                     // Enter: the prompt's row is finished
        renderer.Render(Text("b"));
        renderer.MapClicks([new ClickTarget(0, [ScriptedKeys.Key(ConsoleKey.Enter)])]);

        Assert.NotNull(renderer.KeysAt(2));
        Assert.Null(renderer.KeysAt(1));

        renderer.Clear();
        Assert.Null(renderer.KeysAt(2));
    }

    // ------------------------------------------------------------------ elements

    [Fact]
    public void Menus_TypeTheClickedOptionsKey()
    {
        var (renderer, _) = Terminal();
        var menu = new MenuElement();
        foreach (var (key, label) in new[] { ("1", "Orders"), ("2", "Customers"), ("q", "Quit") })
        {
            var option = new MenuOptionElement { Key = key, Target = label };
            option.AppendText(label);
            menu.Add(option);
        }

        // Lines are read key by key (as the application does with mouse support on), so the click replaces what was typed
        var keys = new MouseDecoder(new ScriptedKeys([Char('z'), ..Click(2, 1)]), renderer);
        var input = new LineEditingInput(keys);

        Assert.Equal("Customers", menu.Choose(renderer, input)!.Target);
    }

    [Fact]
    public void Choices_AreChosenByClicking()
    {
        var (renderer, _) = Terminal();
        renderer.Render(Text("header"));
        var select = new SelectElement { Name = "Colour", Label = "Colour:" };
        foreach (var colour in new[] { "red", "green", "blue" })
        {
            var choice = new ChoiceElement();
            choice.AppendText(colour);
            select.Add(choice);
        }

        // header, label and hint, then red, green, blue
        var answer = select.Read(renderer, new MouseDecoder(new ScriptedKeys(Click(6, 4)), renderer));

        Assert.Equal("blue", answer);
    }

    [Fact]
    public void Checkboxes_TickWhenClicked()
    {
        var (renderer, _) = Terminal();
        var checkbox = new CheckboxElement { Name = "News", Label = "Send news" };

        var answer = checkbox.Read(renderer, new MouseDecoder(new ScriptedKeys([..Click(1, 0), ScriptedKeys.Key(ConsoleKey.Enter)]), renderer));

        Assert.Equal("true", answer);
    }
}
