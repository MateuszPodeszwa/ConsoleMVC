using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for the interactive elements <see cref="MenuElement"/> and <see cref="InputElement"/>.
/// </summary>
public class InteractionTests
{
    private static (MarkupRenderer Renderer, StringWriter Output) PlainRenderer(int width = 40)
    {
        var writer = new StringWriter { NewLine = "\n" };
        return (new MarkupRenderer(new PlainTextRenderTarget(writer), () => width), writer);
    }

    private static MenuOptionElement Option(string key, string label, string? target = null, bool quit = false)
    {
        var option = new MenuOptionElement { Key = key, Target = target, Quit = quit };
        option.AppendText(label);
        return option;
    }

    private static MenuElement Menu(params MenuOptionElement[] options)
    {
        var menu = new MenuElement();
        foreach (var option in options)
            menu.Add(option);
        return menu;
    }

    // --- MenuElement ---

    [Fact]
    public void Menu_Layout_ShowsKeysAndLabels()
    {
        var menu = Menu(Option("1", "About", "Home/About"), Option("q", "Quit", quit: true));

        Assert.Equal(["[1] About", "[q] Quit"], menu.Layout(40).Select(l => l.ToPlainText()));
    }

    [Fact]
    public void Menu_Layout_WrapsLabelsWithHangingIndent()
    {
        var menu = Menu(Option("1", "alpha beta gamma", "A"));

        Assert.Equal(["[1] alpha", "    beta", "    gamma"], menu.Layout(12).Select(l => l.ToPlainText()));
    }

    [Fact]
    public void Menu_Layout_KeysAreBoldAndColoured()
    {
        var menu = Menu(Option("1", "About", "A"));
        menu.KeyColour = Colour.Cyan;

        var key = menu.Layout(40)[0].Spans[0];
        Assert.Equal("[1]", key.Text);
        Assert.Equal(Colour.Cyan, key.Style.Foreground);
        Assert.True(key.Style.Decoration.HasFlag(TextDecoration.Bold));
    }

    [Fact]
    public void Menu_Choose_RepromptsUntilAKeyMatches_CaseInsensitively()
    {
        var (renderer, output) = PlainRenderer();
        var quit = Option("q", "Quit", quit: true);
        var menu = Menu(Option("1", "About", "A"), quit);
        menu.Prompt = "> ";

        var chosen = menu.Choose(renderer, new ScriptedInput("x", " Q "));

        Assert.Same(quit, chosen);
        Assert.Equal("[1] About\n[q] Quit\n> " + MenuElement.DefaultErrorMessage + "\n> ", output.ToString());
    }

    [Fact]
    public void Menu_Choose_EndOfInput_ReturnsNull()
    {
        var (renderer, _) = PlainRenderer();

        Assert.Null(Menu(Option("1", "About", "A")).Choose(renderer, new ScriptedInput()));
    }

    [Fact]
    public void Menu_Add_RejectsDuplicateKeys()
    {
        var menu = Menu(Option("1", "One", "A"));

        Assert.Throws<ArgumentException>(() => menu.Add(Option("1", "Again", "B")));
    }

    // --- InputElement ---

    [Fact]
    public void Input_Read_WritesLabel_AndTrims()
    {
        var (renderer, output) = PlainRenderer();
        var input = new InputElement { Name = "Name", Label = "Name: " };

        Assert.Equal("Ada", input.Read(renderer, new ScriptedInput("  Ada  ")));
        Assert.Equal("Name: ", output.ToString());
    }

    [Fact]
    public void Input_Read_EmptyUsesDefault()
    {
        var (renderer, _) = PlainRenderer();
        var input = new InputElement { Name = "Colour", Default = "blue" };

        Assert.Equal("blue", input.Read(renderer, new ScriptedInput("")));
    }

    [Fact]
    public void Input_Read_Required_RepromptsUntilNotEmpty()
    {
        var (renderer, output) = PlainRenderer();
        var input = new InputElement { Name = "Name", Label = "Name: ", Required = true };

        Assert.Equal("Ada", input.Read(renderer, new ScriptedInput("", "Ada")));
        Assert.Equal("Name: " + InputElement.RequiredMessage + "\nName: ", output.ToString());
    }

    [Fact]
    public void Input_Read_Masked_UsesMaskedReading()
    {
        var (renderer, _) = PlainRenderer();
        var source = new ScriptedInput("secret");

        Assert.Equal("secret", new InputElement { Name = "Password", Masked = true }.Read(renderer, source));
        Assert.Equal(1, source.MaskedReads);
    }

    [Fact]
    public void Input_Read_EndOfInput_ReturnsNull()
    {
        var (renderer, _) = PlainRenderer();

        Assert.Null(new InputElement { Name = "Name", Required = true }.Read(renderer, new ScriptedInput()));
    }

    [Fact]
    public void Input_Label_IsSanitised()
    {
        var (renderer, output) = PlainRenderer();

        new InputElement { Name = "x", Label = "\u001b[2JName: " }.Read(renderer, new ScriptedInput("a"));

        Assert.Equal("Name: ", output.ToString());
    }

    // --- Renderer prompt ---

    [Fact]
    public void Renderer_WritePrompt_WritesWithoutNewLine()
    {
        var (renderer, output) = PlainRenderer();

        renderer.WritePrompt("Name: ", TextStyle.Plain);

        Assert.Equal("Name: ", output.ToString());
    }

    [Fact]
    public void Menu_KeyWithControlCharacters_MatchesWhatIsDisplayed()
    {
        var (renderer, output) = PlainRenderer();
        var option = Option("\u001b[31mX", "Red", "A");
        var menu = Menu(option);

        Assert.Same(option, menu.Choose(renderer, new ScriptedInput("X")));   // the escape sequence is removed whole
        Assert.StartsWith("[X] Red", output.ToString());
    }

    [Fact]
    public void Menu_KeysEqualAfterSanitising_AreDuplicates()
    {
        var menu = Menu(Option("x", "One", "A"));

        Assert.Throws<ArgumentException>(() => menu.Add(Option("\u001bx", "Two", "B")));
    }
}
