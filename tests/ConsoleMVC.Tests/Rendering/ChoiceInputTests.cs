using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c> and <c>&lt;checkbox&gt;</c>: typed answers for piped input and
/// tests, arrow keys in a terminal (docs/design/choice-inputs.md).
/// </summary>
public class ChoiceInputTests
{
    private static SelectElement Colours(string? defaultValue = null, bool multiple = false, bool required = false)
    {
        var select = new SelectElement
        {
            Name = "Colour", Label = "Favourite colour:", Default = defaultValue, Multiple = multiple, Required = required
        };

        foreach (var (value, label) in new[] { ("red", "Red"), ("green", "Green"), ("blue", "Blue") })
        {
            var choice = new ChoiceElement { Value = value };
            choice.AppendText(label);
            select.Add(choice);
        }

        return select;
    }

    private static RadioElement Sizes(string? defaultValue = null)
    {
        var radio = new RadioElement { Name = "Size", Label = "Size:", Default = defaultValue };
        foreach (var label in new[] { "S", "M", "L" })
        {
            var choice = new ChoiceElement();
            choice.AppendText(label);
            radio.Add(choice);
        }

        return radio;
    }

    private static (MarkupRenderer Renderer, StringWriter Output) Typed()
    {
        var output = new StringWriter();
        return (new MarkupRenderer(new PlainTextRenderTarget(output), () => 60), output);
    }

    private static (MarkupRenderer Renderer, RewritableScreen Screen) Keyed()
    {
        var screen = new RewritableScreen();
        var renderer = new MarkupRenderer(screen, () => 60);
        renderer.Clear();
        return (renderer, screen);
    }

    // ------------------------------------------------------------------ typed answers

    [Theory]
    [InlineData("2", "green")]
    [InlineData(" blue ", "blue")]
    [InlineData("RED", "red")]
    [InlineData("Green", "green")]
    public void Select_AcceptsANumberAValueOrALabel(string line, string expected)
    {
        var (renderer, _) = Typed();

        Assert.Equal(expected, Colours().Read(renderer, new ScriptedInput(line)));
    }

    [Fact]
    public void Select_ShowsNumberedChoicesAndThePrompt()
    {
        var (renderer, output) = Typed();

        Colours(defaultValue: "green").Read(renderer, new ScriptedInput("3"));

        Assert.Equal("Favourite colour:\n  1) Red\n  2) Green\n  3) Blue\nChoose 1-3 [Green]: ", output.ToString());
    }

    [Fact]
    public void Select_EmptyTakesTheDefault_OrNothing()
    {
        var (renderer, _) = Typed();

        Assert.Equal("green", Colours(defaultValue: "green").Read(renderer, new ScriptedInput("")));
        Assert.Equal("", Colours().Read(renderer, new ScriptedInput("")));
    }

    [Fact]
    public void Select_AsksAgain_ForAnUnknownChoice_OrAnEmptyRequiredOne()
    {
        var (renderer, output) = Typed();

        var value = Colours(required: true).Read(renderer, new ScriptedInput("purple", "", "4", "1"));

        Assert.Equal("red", value);
        Assert.Contains("'purple' is not one of the choices.", output.ToString());
        Assert.Contains(InputElement.RequiredMessage, output.ToString());
        Assert.Contains("'4' is not one of the choices.", output.ToString());
    }

    [Fact]
    public void Select_Multiple_JoinsTheValuesWithCommas()
    {
        var (renderer, output) = Typed();

        var value = Colours(multiple: true).Read(renderer, new ScriptedInput("3, 1,3"));

        Assert.Equal("red,blue", value); // in the order of the choices, without duplicates
        Assert.Contains("Choose one or more of 1-3, e.g. 1,3: ", output.ToString());
    }

    [Fact]
    public void Select_EndOfInput_ReturnsNull()
    {
        var (renderer, _) = Typed();

        Assert.Null(Colours().Read(renderer, new ScriptedInput()));
    }

    [Fact]
    public void Select_WithoutChoices_Throws()
    {
        var (renderer, _) = Typed();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new SelectElement { Name = "Empty" }.Read(renderer, new ScriptedInput("1")));
        Assert.Contains("Empty", error.Message);
    }

    [Fact]
    public void Multiple_RejectsValuesWithCommas()
    {
        var (renderer, _) = Typed();
        var select = new SelectElement { Name = "Tags", Multiple = true };
        select.Add(new ChoiceElement { Value = "a,b" });

        Assert.Throws<InvalidOperationException>(() => select.Read(renderer, new ScriptedInput("1")));
    }

    [Fact]
    public void Add_RejectsDuplicateValues()
    {
        var select = Colours();

        Assert.Throws<ArgumentException>(() => select.Add(new ChoiceElement { Value = "RED" }));
    }

    [Fact]
    public void Choice_ValueDefaultsToItsLabel()
    {
        var (renderer, _) = Typed();

        Assert.Equal("M", Sizes().Read(renderer, new ScriptedInput("m")));
    }

    [Fact]
    public void Radio_ShowsTheChoicesOnOneLine()
    {
        var (renderer, output) = Typed();

        Sizes().Read(renderer, new ScriptedInput("2"));

        Assert.Equal("Size: 1) S  2) M  3) L\nChoose 1-3: ", output.ToString());
    }

    [Theory]
    [InlineData("y", "true")]
    [InlineData("YES", "true")]
    [InlineData("n", "false")]
    [InlineData("", "false")]
    public void Checkbox_AcceptsYesOrNo(string line, string expected)
    {
        var (renderer, output) = Typed();

        Assert.Equal(expected, new CheckboxElement { Name = "News", Label = "Send news" }.Read(renderer, new ScriptedInput(line)));
        Assert.Equal("Send news? [y/N]: ", output.ToString());
    }

    [Fact]
    public void Checkbox_Checked_IsTheDefault()
    {
        var (renderer, output) = Typed();

        Assert.Equal("true", new CheckboxElement { Name = "News", Label = "Send news", Checked = true }.Read(renderer, new ScriptedInput("")));
        Assert.Equal("Send news? [Y/n]: ", output.ToString());
    }

    [Fact]
    public void Checkbox_Required_MustBeTicked_AndAnswersMustBeYesOrNo()
    {
        var (renderer, output) = Typed();
        var terms = new CheckboxElement { Name = "Terms", Label = "I accept the terms", Required = true };

        Assert.Equal("true", terms.Read(renderer, new ScriptedInput("maybe", "n", "y")));
        Assert.Contains(CheckboxElement.AnswerMessage, output.ToString());
        Assert.Contains(CheckboxElement.RequiredMessage, output.ToString());
    }

    // ------------------------------------------------------------------ arrow keys

    [Fact]
    public void Keys_MoveTheHighlightAndEnterChooses()
    {
        var (renderer, screen) = Keyed();
        var value = Colours().Read(renderer, new ScriptedKeys(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow,
            ConsoleKey.UpArrow, ConsoleKey.Enter));

        Assert.Equal("blue", value);
        Assert.Equal("Favourite colour: Blue\n", screen.Screen); // collapsed to the answer, in place
        Assert.True(screen.Erases >= 5);
    }

    [Fact]
    public void Keys_StartOnTheDefault_AndWrapAround()
    {
        var (renderer, _) = Keyed();

        Assert.Equal("red", Colours(defaultValue: "blue").Read(renderer, new ScriptedKeys(ConsoleKey.DownArrow, ConsoleKey.Enter)));
    }

    [Fact]
    public void Keys_ShowTheHighlightedChoice()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ConsoleKey.DownArrow);

        Colours().Read(renderer, keys); // the input ends after one key

        Assert.Equal("Favourite colour: (Up/Down, Enter)\n   Red\n > Green\n   Blue\n", screen.Screen);
    }

    [Fact]
    public void Keys_DigitsJumpToAChoice()
    {
        var (renderer, _) = Keyed();

        Assert.Equal("green", Colours().Read(renderer, new ScriptedKeys(ScriptedKeys.Char('2'), ScriptedKeys.Key(ConsoleKey.Enter))));
    }

    [Fact]
    public void Keys_Multiple_SpaceTicks()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ConsoleKey.Spacebar, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Spacebar,
            ConsoleKey.UpArrow, ConsoleKey.UpArrow, ConsoleKey.Spacebar, ConsoleKey.Spacebar, ConsoleKey.Enter);

        Assert.Equal("red,blue", Colours(multiple: true).Read(renderer, keys));
        Assert.Equal("Favourite colour: Red, Blue\n", screen.Screen);
    }

    [Fact]
    public void Keys_Multiple_Required_ShowsAnErrorUntilSomethingIsTicked()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ConsoleKey.Enter);

        Assert.Null(Colours(multiple: true, required: true).Read(renderer, keys));
        Assert.EndsWith(InputElement.RequiredMessage + "\n", screen.Screen);
    }

    [Fact]
    public void Keys_Radio_UsesLeftAndRight()
    {
        var (renderer, screen) = Keyed();

        Assert.Equal("L", Sizes(defaultValue: "M").Read(renderer, new ScriptedKeys(ConsoleKey.RightArrow, ConsoleKey.Enter)));
        Assert.Equal("Size: L\n", screen.Screen);
    }

    [Fact]
    public void Keys_Checkbox_SpaceToggles()
    {
        var (renderer, screen) = Keyed();
        var news = new CheckboxElement { Name = "News", Label = "Send news" };

        Assert.Equal("true", news.Read(renderer, new ScriptedKeys(ConsoleKey.Spacebar, ConsoleKey.Enter)));
        Assert.Equal("[x] Send news\n", screen.Screen);
    }

    [Fact]
    public void Keys_Checkbox_YAndNAnswerAtOnce()
    {
        var (renderer, _) = Keyed();
        var news = new CheckboxElement { Name = "News", Label = "Send news", Checked = true };

        Assert.Equal("false", news.Read(renderer, new ScriptedKeys(ScriptedKeys.Char('n'))));
    }

    [Fact]
    public void Keys_AreNotUsed_WhenTheRendererCannotRewrite()
    {
        // A plain-text target cannot move the cursor, so the typed form is used even if keys could be read
        var (renderer, _) = Typed();

        Assert.Throws<InvalidOperationException>(() => Colours().Read(renderer, new ScriptedKeys(ConsoleKey.Enter)));
    }

    [Fact]
    public void Keys_Resize_RedrawsTheLatestState()
    {
        var screen = new RewritableScreen();
        var width = 60;
        var renderer = new MarkupRenderer(screen, () => width);
        renderer.Clear();
        var keys = new ScriptedKeys(ConsoleKey.DownArrow);

        Colours().Read(renderer, keys);
        width = 40;
        Assert.True(renderer.RedrawIfWidthChanged());

        Assert.Equal("Favourite colour: (Up/Down, Enter)\n   Red\n > Green\n   Blue\n", screen.Screen);
    }

    // ------------------------------------------------------------------ renderer

    [Fact]
    public void Rewrite_ReplacesTheLastElementInPlace()
    {
        var (renderer, screen) = Keyed();
        var first = new TextElement();
        first.AppendText("one");
        var second = new TextElement();
        second.AppendText("two\nlines");

        Assert.True(renderer.CanRewrite);
        renderer.Render(first);
        renderer.Rewrite(second);

        Assert.Equal("two\nlines\n", screen.Screen);
    }

    [Fact]
    public void Rewrite_AfterAPrompt_RedrawsTheScreen()
    {
        var (renderer, screen) = Keyed();
        var first = new TextElement();
        first.AppendText("one");
        var second = new TextElement();
        second.AppendText("two");

        renderer.Render(first);
        renderer.WritePrompt("> ", TextStyle.Plain);
        renderer.Rewrite(second);

        Assert.Equal("two\n> ", screen.Screen);
    }

    [Fact]
    public void PlainTextTarget_CannotRewrite()
    {
        var (renderer, output) = Typed();
        var text = new TextElement();
        text.AppendText("x");

        Assert.False(renderer.CanRewrite);
        renderer.Rewrite(text);

        Assert.Equal("x\n", output.ToString());
    }
}
