using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for <c>&lt;number&gt;</c>, <c>&lt;date&gt;</c> and <c>&lt;select filter size&gt;</c>
/// (docs/design/richer-inputs.md).
/// </summary>
public class RicherInputTests
{
    /// <summary>A clock fixed at 27 October 2026 (a Tuesday).</summary>
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static (MarkupRenderer Renderer, StringWriter Output) Typed()
    {
        var output = new StringWriter { NewLine = "\n" };
        return (new MarkupRenderer(new PlainTextRenderTarget(output), () => 60), output);
    }

    private static (MarkupRenderer Renderer, RewritableScreen Screen) Keyed()
    {
        var screen = new RewritableScreen();
        var renderer = new MarkupRenderer(screen, () => 60);
        renderer.Clear();
        return (renderer, screen);
    }

    private static NumberElement Age() => new() { Name = "Age", Label = "Age: ", Min = 0, Max = 130, Decimals = 0 };

    private static DateElement Due(string? min = null, string? max = null, string? defaultValue = null) =>
        new() { Name = "Due", Label = "Due: ", Min = min, Max = max, Default = defaultValue, Clock = new FixedClock() };

    // ------------------------------------------------------------------ <number>

    [Theory]
    [InlineData("42", "42")]
    [InlineData(" 7 ", "7")]
    [InlineData("0", "0")]
    [InlineData("130", "130")]
    public void Number_AcceptsValuesInRange(string line, string expected)
    {
        var (renderer, _) = Typed();

        Assert.Equal(expected, Age().Read(renderer, new ScriptedInput(line)));
    }

    [Fact]
    public void Number_AsksAgain_WithAMessageForEachProblem()
    {
        var (renderer, output) = Typed();

        Assert.Equal("30", Age().Read(renderer, new ScriptedInput("abc", "2.5", "131", "30")));

        Assert.Contains(NumberElement.NotANumberMessage, output.ToString());
        Assert.Contains(NumberElement.WholeNumberMessage, output.ToString());
        Assert.Contains("Enter a number from 0 to 130.", output.ToString());
    }

    [Fact]
    public void Number_Decimals_AndTheInvariantCulture()
    {
        var (renderer, output) = Typed();
        var price = new NumberElement { Name = "Price", Label = "Price:", Decimals = 2, Min = 0 };

        Assert.Equal("2.5", price.Read(renderer, new ScriptedInput("1,5", "2.555", "2.50")));
        Assert.Contains("at most 2 decimal places", output.ToString());
        Assert.StartsWith("Price: ", output.ToString()); // a space after the label
    }

    [Fact]
    public void Number_EmptyTakesTheDefault_OrIsRequired()
    {
        var (renderer, output) = Typed();

        Assert.Equal("30", new NumberElement { Name = "A", Default = "30" }.Read(renderer, new ScriptedInput("")));
        Assert.Equal("", new NumberElement { Name = "A" }.Read(renderer, new ScriptedInput("")));
        Assert.Equal("1", new NumberElement { Name = "A", Required = true }.Read(renderer, new ScriptedInput("", "1")));
        Assert.Contains(InputElement.RequiredMessage, output.ToString());
    }

    [Fact]
    public void Number_Keys_StepWithinTheRange_AndDigitsReplaceTheValue()
    {
        var (renderer, screen) = Keyed();
        var age = Age();
        age.Default = "129";

        var keys = new ScriptedKeys(ConsoleKey.UpArrow, ConsoleKey.UpArrow, ConsoleKey.UpArrow, ConsoleKey.Enter);
        Assert.Equal("130", age.Read(renderer, keys)); // stays at the maximum
        Assert.Equal("Age: 130\n", screen.Screen);

        var (renderer2, _) = Keyed();
        var typed = new ScriptedKeys(ScriptedKeys.Char('4'), ScriptedKeys.Char('2'), ScriptedKeys.Key(ConsoleKey.DownArrow),
            ScriptedKeys.Key(ConsoleKey.Enter));
        Assert.Equal("41", Age().Read(renderer2, typed));
    }

    [Fact]
    public void Number_Keys_ShowTheErrorUntilTheValueIsValid()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ScriptedKeys.Char('2'), ScriptedKeys.Char('0'), ScriptedKeys.Char('0'), ScriptedKeys.Key(ConsoleKey.Enter));

        Assert.Null(Age().Read(renderer, keys)); // the input ends while the error is shown

        Assert.Contains("Enter a number from 0 to 130.", screen.Screen);
        Assert.StartsWith("Age: 200  (Up/Down, type a number, Enter)", screen.Screen);
    }

    [Fact]
    public void Number_MinAfterMax_Throws()
    {
        var (renderer, _) = Typed();

        Assert.Throws<InvalidOperationException>(() =>
            new NumberElement { Name = "A", Min = 5, Max = 1 }.Read(renderer, new ScriptedInput("3")));
    }

    // ------------------------------------------------------------------ <date>

    [Theory]
    [InlineData("2026-12-25", "2026-12-25")]
    [InlineData("today", "2026-10-27")]
    [InlineData("Tomorrow", "2026-10-28")]
    [InlineData("yesterday", "2026-10-26")]
    [InlineData("+7", "2026-11-03")]
    [InlineData("-1", "2026-10-26")]
    [InlineData("today+30", "2026-11-26")]
    public void Date_AcceptsDatesAndRelativeDays(string line, string expected)
    {
        var (renderer, _) = Typed();

        Assert.Equal(expected, Due().Read(renderer, new ScriptedInput(line)));
    }

    [Fact]
    public void Date_ChecksTheFormAndTheRange()
    {
        var (renderer, output) = Typed();

        Assert.Equal("2026-11-01", Due(min: "today", max: "today+30").Read(renderer,
            new ScriptedInput("27/10/2026", "2026-02-30", "2026-10-01", "2027-01-01", "2026-11-01")));

        Assert.Contains(DateElement.NotADateMessage, output.ToString().Replace('\n', ' '));
        Assert.Contains("Enter a date from 2026-10-27 to 2026-11-26.", output.ToString());
    }

    [Fact]
    public void Date_EmptyTakesTheDefault()
    {
        var (renderer, _) = Typed();

        Assert.Equal("2026-11-03", Due(defaultValue: "today+7").Read(renderer, new ScriptedInput("")));
        Assert.Equal("", Due().Read(renderer, new ScriptedInput("")));
    }

    [Fact]
    public void Date_Keys_ChangeThePartsAndStayInRange()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ConsoleKey.UpArrow, ConsoleKey.LeftArrow, ConsoleKey.UpArrow, ConsoleKey.UpArrow,
            ConsoleKey.LeftArrow, ConsoleKey.UpArrow, ConsoleKey.Enter);

        // day +1 → 28 Oct, month +2 → 28 Dec, year +1 → clamped to max 2027-01-31
        Assert.Equal("2027-01-31", Due(max: "2027-01-31").Read(renderer, keys));
        Assert.Equal("Due: 2027-01-31\n", screen.Screen);
    }

    [Fact]
    public void Date_Keys_ShowTheDate_AndTGoesToToday()
    {
        var (renderer, screen) = Keyed();

        Assert.Null(Due(defaultValue: "2026-01-31").Read(renderer, new ScriptedKeys(ConsoleKey.LeftArrow, ConsoleKey.UpArrow)));
        Assert.Equal("Due: 2026-02-28 Sat  (Left/Right, Up/Down, t today, Enter)\n", screen.Screen); // the day fits the month

        var (renderer2, _) = Keyed();
        Assert.Equal("2026-10-27", Due(defaultValue: "2020-01-01").Read(renderer2,
            new ScriptedKeys(ScriptedKeys.Char('t'), ScriptedKeys.Key(ConsoleKey.Enter))));
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("2026-13-01")]
    public void Date_InvalidSettings_Throw(string setting)
    {
        var (renderer, _) = Typed();

        var error = Assert.Throws<InvalidOperationException>(() => Due(min: setting).Read(renderer, new ScriptedInput("today")));
        Assert.Contains(setting, error.Message);
    }

    // ------------------------------------------------------------------ <select filter size>

    private static SelectElement Countries(bool filter = true, int size = 0, bool multiple = false)
    {
        var select = new SelectElement { Name = "Country", Label = "Country:", Filter = filter, Size = size, Multiple = multiple };
        foreach (var (code, name) in new[] { ("FR", "France"), ("DE", "Germany"), ("GR", "Greece"), ("IT", "Italy"), ("PL", "Poland"), ("PT", "Portugal"), ("ES", "Spain") })
        {
            var choice = new ChoiceElement { Value = code };
            choice.AppendText(name);
            select.Add(choice);
        }

        return select;
    }

    [Fact]
    public void Filter_Keys_TypingNarrowsTheChoices()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ScriptedKeys.Char('p'), ScriptedKeys.Char('o'), ScriptedKeys.Key(ConsoleKey.DownArrow));

        Assert.Null(Countries().Read(renderer, keys));

        Assert.Equal("Country: (type to filter, Up/Down, Enter)\n Filter: po\n   Poland\n > Portugal\n", screen.Screen);
    }

    [Fact]
    public void Filter_Keys_BackspaceAndEnter()
    {
        var (renderer, _) = Keyed();
        var keys = new ScriptedKeys(ScriptedKeys.Char('g'), ScriptedKeys.Char('x'), ScriptedKeys.Key(ConsoleKey.Backspace),
            ScriptedKeys.Key(ConsoleKey.DownArrow), ScriptedKeys.Key(ConsoleKey.Enter));

        Assert.Equal("GR", Countries().Read(renderer, keys)); // "g" matches Germany, Greece and Portugal
    }

    [Fact]
    public void Filter_Keys_NoMatch_ShowsAMessage()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ScriptedKeys.Char('z'), ScriptedKeys.Key(ConsoleKey.Enter));

        Assert.Null(Countries().Read(renderer, keys));

        Assert.Contains("(no matches)", screen.Screen);
        Assert.Contains("No choice matches 'z'.", screen.Screen);
    }

    [Fact]
    public void Filter_Typed_PartOfANameChoosesTheOnlyMatch()
    {
        var (renderer, output) = Typed();

        Assert.Equal("ES", Countries().Read(renderer, new ScriptedInput("g", "spa")));
        Assert.Contains("'g' matches Germany, Greece, Portugal; type more of the name.", output.ToString().Replace('\n', ' '));
    }

    [Fact]
    public void Filter_Typed_IsOffWithoutTheAttribute()
    {
        var (renderer, output) = Typed();

        Assert.Equal("ES", Countries(filter: false).Read(renderer, new ScriptedInput("spa", "Spain")));
        Assert.Contains("'spa' is not one of the choices.", output.ToString());
    }

    [Fact]
    public void Size_Keys_ShowsAWindowAroundTheHighlight()
    {
        var (renderer, screen) = Keyed();
        var keys = new ScriptedKeys(ConsoleKey.End);

        Assert.Null(Countries(filter: false, size: 3).Read(renderer, keys));

        Assert.Equal("Country: (Up/Down, Enter)\n   (4 more above)\n   Poland\n   Portugal\n > Spain\n", screen.Screen);
    }

    [Fact]
    public void Size_Keys_PageDownMovesAPage()
    {
        var (renderer, _) = Keyed();

        Assert.Equal("IT", Countries(filter: false, size: 3).Read(renderer, new ScriptedKeys(ConsoleKey.PageDown, ConsoleKey.Enter)));
    }

    [Fact]
    public void Size_CannotBeNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectElement { Size = -1 });
    }
}
