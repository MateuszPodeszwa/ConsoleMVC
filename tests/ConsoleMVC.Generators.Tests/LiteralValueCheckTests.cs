using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Rendering;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Literal attribute values that the runtime element cannot use are build errors, and the build agrees with the runtime:
/// each rejected value makes the runtime element throw (or misbehave), and each value at the boundary that the build
/// accepts works when the view runs.
/// </summary>
public class LiteralValueCheckTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    /// <summary>Shows the stored answer of field <c>n</c>, so a test can see what the view did with it.</summary>
    private const string ShowAnswer = "\n<text>answer={@Form[\"n\"]}</text>" + Quit;

    private static Diagnostic Single(string cvw, string id)
    {
        var result = ViewHarness.Generate(cvw);
        var matches = result.GeneratorDiagnostics.Where(d => d.Id == id).ToList();

        Assert.True(matches.Count == 1, $"Expected one {id}:\n{result.Describe()}");
        Assert.Null(result.GeneratedSource);
        return matches[0];
    }

    private static (int Line, int Column) Position(Diagnostic diagnostic)
    {
        var start = diagnostic.Location.GetLineSpan().StartLinePosition;
        return (start.Line, start.Character);
    }

    /// <summary>The column of the value of the <paramref name="occurrence"/>th <c>attribute="</c> in <paramref name="markup"/>.</summary>
    private static int ValueColumn(string markup, string attribute, int occurrence = 1)
    {
        var index = -1;
        for (var i = 0; i < occurrence; i++)
            index = markup.IndexOf(" " + attribute + "=\"", index + 1, StringComparison.Ordinal);

        Assert.True(index >= 0, $"'{attribute}' not found in {markup}");
        return index + attribute.Length + 3;
    }

    private static void AssertRejectedAt(string markup, string attribute, string id = "CVW101", int occurrence = 1)
    {
        var diagnostic = Single(markup + Quit, id);
        Assert.Equal((0, ValueColumn(markup, attribute, occurrence)), Position(diagnostic));
    }

    private static string Answer(string markup, params string[] input)
    {
        var output = ViewHarness.Render(markup + ShowAnswer, input: input);
        var start = output.IndexOf("answer={", StringComparison.Ordinal) + "answer={".Length;
        return output[start..output.IndexOf('}', start)];
    }

    private static (IMarkupRenderer Renderer, ITextInput Input) Terminal(params string[] lines) =>
        (new MarkupRenderer(new PlainTextRenderTarget(new StringWriter()), () => 40), new Lines(lines));

    private sealed class Lines(string[] lines) : ITextInput
    {
        private readonly Queue<string> _lines = new(lines);

        public string? ReadLine() => _lines.Count > 0 ? _lines.Dequeue() : null;

        public string? ReadMasked(char mask) => ReadLine();
    }

    // ------------------------------------------------------------------ <number>

    [Theory]
    [InlineData("<number name=\"n\" step=\"0\" />", "step")]
    [InlineData("<number name=\"n\" step=\"-1\" />", "step")]
    [InlineData("<number name=\"n\" step=\"0.000\" />", "step")]
    public void Number_StepThatIsNotPositive_IsABuildError(string markup, string attribute) =>
        AssertRejectedAt(markup, attribute);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Number_StepThatIsNotPositive_IsRefusedByTheRuntime(string step) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberElement { Step = decimal.Parse(step, System.Globalization.CultureInfo.InvariantCulture) });

    [Fact]
    public void Number_SmallestPositiveStep_Builds_AndRuns()
    {
        Assert.Equal("7", Answer("<number name=\"n\" step=\"0.001\" />", "7"));
        Assert.Equal(0.001m, new NumberElement { Step = 0.001m }.Step);
    }

    [Fact]
    public void Number_MinAboveMax_IsABuildError() =>
        AssertRejectedAt("<number name=\"n\" min=\"10\" max=\"1\" />", "min");

    [Fact]
    public void Number_MinAboveMax_IsRefusedByTheRuntime()
    {
        var (renderer, input) = Terminal("5");
        Assert.Throws<InvalidOperationException>(() => new NumberElement { Name = "n", Min = 10, Max = 1 }.Read(renderer, input));
    }

    [Fact]
    public void Number_MinEqualToMax_Builds_AndRuns() =>
        Assert.Equal("5", Answer("<number name=\"n\" min=\"5\" max=\"5\" />", "5"));

    [Theory]
    [InlineData("<number name=\"n\" default=\"abc\" />")]
    [InlineData("<number name=\"n\" min=\"1\" max=\"5\" default=\"9\" />")]
    [InlineData("<number name=\"n\" min=\"1\" max=\"5\" default=\"0.99\" />")]
    [InlineData("<number name=\"n\" min=\"1\" default=\"0\" />")]
    [InlineData("<number name=\"n\" max=\"5\" default=\"5.01\" />")]
    [InlineData("<number name=\"n\" decimals=\"0\" default=\"2.5\" />")]
    [InlineData("<number name=\"n\" decimals=\"1\" default=\"2.25\" />")]
    public void Number_DefaultThatIsNotAnAcceptedNumber_IsABuildError(string markup) =>
        AssertRejectedAt(markup, "default");

    [Theory]
    [InlineData("abc", null, null, null)]
    [InlineData("9", "1", "5", null)]
    [InlineData("0.99", "1", "5", null)]
    [InlineData("2.5", null, null, 0)]
    [InlineData("2.25", null, null, 1)]
    public void Number_DefaultThatIsNotAnAcceptedNumber_IsRefusedByTheRuntime_InsteadOfAskingForEver(string fallback, string? min,
        string? max, int? decimals)
    {
        var field = new NumberElement
        {
            Name = "n", Default = fallback, Decimals = decimals,
            Min = min is null ? null : decimal.Parse(min, System.Globalization.CultureInfo.InvariantCulture),
            Max = max is null ? null : decimal.Parse(max, System.Globalization.CultureInfo.InvariantCulture)
        };
        var (renderer, input) = Terminal("", "", "");

        Assert.Throws<InvalidOperationException>(() => field.Read(renderer, input));
    }

    [Theory]
    [InlineData("<number name=\"n\" min=\"1\" max=\"5\" default=\"1\" />", "1")]
    [InlineData("<number name=\"n\" min=\"1\" max=\"5\" default=\"5\" />", "5")]
    [InlineData("<number name=\"n\" min=\"5\" max=\"5\" default=\"5\" />", "5")]
    [InlineData("<number name=\"n\" decimals=\"0\" default=\"2\" />", "2")]
    [InlineData("<number name=\"n\" decimals=\"1\" default=\"2.5\" />", "2.5")]
    [InlineData("<number name=\"n\" default=\" -3 \" />", "-3")]
    [InlineData("<number name=\"n\" default=\"\" />", "")]
    [InlineData("<number name=\"n\" default=\"  \" />", "")]
    public void Number_DefaultAtTheBoundary_Builds_AndAnswersAnEmptyLine(string markup, string expected) =>
        Assert.Equal(expected, Answer(markup, ""));

    [Fact]
    public void Number_DefaultIsOnlyCheckedAgainstTheLiteralParts_WhenTheRangeIsAnExpression()
    {
        // max is an expression, so only min is known when building; the runtime then checks the whole range
        Assert.Equal("4", Answer("var top = 10m;\n<number name=\"n\" min=\"1\" max=\"@top\" default=\"4\" />", ""));
        AssertRejectedAt("<number name=\"n\" min=\"1\" max=\"@Top\" default=\"0\" />", "default");
    }

    // ------------------------------------------------------------------ <select>, <radio>, <choice>

    [Theory]
    [InlineData("<select name=\"n\" default=\"zzz\"><choice value=\"a\">A</choice><choice value=\"b\">B</choice></select>")]
    [InlineData("<radio name=\"n\" default=\"zzz\"><choice>S</choice><choice>M</choice></radio>")]
    [InlineData("<select name=\"n\" default=\"Apple\"><choice value=\"1\">Apple</choice></select>")]
    [InlineData("<select name=\"n\" multiple default=\"a,zzz\"><choice value=\"a\">A</choice><choice value=\"b\">B</choice></select>")]
    [InlineData("<select name=\"n\" default=\"a,b\"><choice value=\"a\">A</choice><choice value=\"b\">B</choice></select>")]
    public void Choice_DefaultThatIsNotAChoice_IsABuildError(string markup) =>
        AssertRejectedAt(markup, "default");

    [Theory]
    [InlineData("zzz", false)]
    [InlineData("a,zzz", true)]
    [InlineData("a,b", false)]
    public void Choice_DefaultThatIsNotAChoice_IsRefusedByTheRuntime_InsteadOfStoringIt(string fallback, bool multiple)
    {
        var field = new SelectElement { Name = "n", Default = fallback, Multiple = multiple };
        field.Add(new ChoiceElement { Value = "a" });
        field.Add(new ChoiceElement { Value = "b" });
        var (renderer, input) = Terminal("");

        Assert.Throws<InvalidOperationException>(() => field.Read(renderer, input));
    }

    [Theory]
    [InlineData("<select name=\"n\" default=\"B\"><choice value=\"a\">A</choice><choice value=\"b\">B</choice></select>", "b")]
    [InlineData("<radio name=\"n\" default=\"m\"><choice>S</choice><choice>M</choice></radio>", "M")]
    [InlineData("<select name=\"n\" multiple default=\" b , a \"><choice value=\"a\">A</choice><choice value=\"b\">B</choice></select>", "a,b")]
    [InlineData("<select name=\"n\" default=\"   \"><choice value=\"a\">A</choice></select>", "")]
    public void Choice_DefaultThatNamesAChoice_Builds_AndAnswersAnEmptyLine(string markup, string expected) =>
        Assert.Equal(expected, Answer(markup, ""));

    [Fact]
    public void Choice_DefaultIsNotChecked_WhenTheChoicesAreNotAllKnownWhenBuilding()
    {
        Assert.Equal("c", Answer("var extra = \"c\";\n<select name=\"n\" default=\"c\"><choice value=\"a\">A</choice><choice value=\"@extra\">C</choice></select>", ""));
        Assert.Equal("c", Answer("var items = new[] { \"c\" };\n<select name=\"n\" default=\"c\">@foreach (var item in items) { <choice>@item</choice> }</select>", ""));
    }

    [Theory]
    [InlineData("<select name=\"n\"><choice value=\"a\">A</choice><choice value=\"A\">Also A</choice></select>", 2)]
    [InlineData("<radio name=\"n\"><choice value=\"x\">X</choice><choice value=\"y\">Y</choice><choice value=\"x\">X again</choice></radio>", 3)]
    public void Choice_DuplicateLiteralValue_IsABuildError_AtTheLaterValue(string markup, int duplicate) =>
        AssertRejectedAt(markup, "value", occurrence: duplicate);

    [Fact]
    public void Choice_DuplicateLabel_WithoutAValue_IsABuildError_AtTheSecondChoice()
    {
        const string markup = "<radio name=\"n\"><choice>Red</choice><choice> red </choice></radio>";
        var diagnostic = Single(markup + Quit, "CVW109");
        Assert.Equal((0, markup.LastIndexOf("<choice>", StringComparison.Ordinal)), Position(diagnostic));
    }

    [Fact]
    public void Choice_DuplicateValue_IsRefusedByTheRuntime()
    {
        var field = new RadioElement { Name = "n" };
        field.Add(new ChoiceElement { Value = "a" });
        Assert.Throws<ArgumentException>(() => field.Add(new ChoiceElement { Value = "A" }));
    }

    [Fact]
    public void Choice_ValuesThatDiffer_Build_AndRun() =>
        Assert.Equal("b", Answer("<radio name=\"n\"><choice value=\"a\">A</choice><choice value=\"b\">A</choice></radio>", "2"));

    [Fact]
    public void Choice_CommaInAValue_OfAMultipleSelect_IsABuildError_AndRefusedByTheRuntime()
    {
        AssertRejectedAt("<select name=\"n\" multiple><choice value=\"a,b\">AB</choice></select>", "value");

        var field = new SelectElement { Name = "n", Multiple = true };
        field.Add(new ChoiceElement { Value = "a,b" });
        var (renderer, input) = Terminal("1");
        Assert.Throws<InvalidOperationException>(() => field.Read(renderer, input));

        // Without multiple a comma is an ordinary character
        Assert.Equal("a,b", Answer("<select name=\"n\"><choice value=\"a,b\">AB</choice></select>", "1"));
    }

    [Fact]
    public void Choice_FieldWithNoChoices_IsABuildError_AndRefusedByTheRuntime()
    {
        const string markup = "<select name=\"n\"></select>";
        Assert.Equal((0, 0), Position(Single(markup + Quit, "CVW109")));

        var (renderer, input) = Terminal("1");
        Assert.Throws<InvalidOperationException>(() => new SelectElement { Name = "n" }.Read(renderer, input));
    }

    // ------------------------------------------------------------------ <menu>

    [Theory]
    [InlineData("<menu><option key=\"q\" quit>Quit</option><option key=\"Q\" back>Back</option></menu>")]
    [InlineData("<menu><option key=\"1\" back>A</option><option key=\" 1 \" quit>B</option></menu>")]
    public void Menu_DuplicateKey_IsABuildError_AtTheSecondKey(string markup)
    {
        var diagnostic = Single(markup, "CVW101");
        Assert.Equal((0, ValueColumn(markup, "key", 2)), Position(diagnostic));
    }

    [Fact]
    public void Menu_EmptyKey_IsABuildError()
    {
        const string markup = "<menu><option key=\" \" quit>Quit</option></menu>";
        Assert.Equal((0, ValueColumn(markup, "key")), Position(Single(markup, "CVW101")));
    }

    [Fact]
    public void Menu_DuplicateOrEmptyKey_IsRefusedByTheRuntime()
    {
        var menu = new MenuElement();
        menu.Add(new MenuOptionElement { Key = "q" });
        Assert.Throws<ArgumentException>(() => menu.Add(new MenuOptionElement { Key = "Q" }));
        Assert.Throws<ArgumentException>(() => menu.Add(new MenuOptionElement { Key = " " }));
    }

    [Fact]
    public void Menu_KeysThatDiffer_Build_AndRun()
    {
        var (_, navigation) = ViewHarness.RenderWithNavigation(
            "<menu><option key=\"q\" quit>Quit</option><option key=\"w\" back>Back</option></menu>", input: "W");
        Assert.True(navigation.IsBack);
    }

    [Fact]
    public void Menu_KeysAreNotChecked_WhenOptionsComeFromCode() =>
        Assert.Null(ViewHarness.Generate("var keys = new[] { \"a\", \"b\" };\n<menu>@foreach (var k in keys) { <option key=\"@k\" back>@k</option> }<option key=\"q\" quit>Quit</option></menu>")
            .Errors.FirstOrDefault());

    // ------------------------------------------------------------------ <date>

    [Theory]
    [InlineData("<date name=\"n\" min=\"today+5\" max=\"today\" />")]
    [InlineData("<date name=\"n\" min=\"2026-02-01\" max=\"2026-01-31\" />")]
    [InlineData("<date name=\"n\" min=\"tomorrow\" max=\"today\" />")]
    public void Date_MinAfterMax_IsABuildError(string markup) =>
        AssertRejectedAt(markup, "min");

    [Fact]
    public void Date_MinAfterMax_IsRefusedByTheRuntime()
    {
        var (renderer, input) = Terminal("");
        Assert.Throws<InvalidOperationException>(() => new DateElement { Name = "n", Min = "today+5", Max = "today" }.Read(renderer, input));
    }

    [Theory]
    [InlineData("<date name=\"n\" min=\"today\" max=\"today\" default=\"today\" />")]
    [InlineData("<date name=\"n\" min=\"2026-01-31\" max=\"2026-01-31\" default=\"2026-01-31\" />")]
    [InlineData("<date name=\"n\" min=\"yesterday\" max=\"tomorrow\" default=\"+1\" />")]
    [InlineData("<date name=\"n\" default=\"today+100000\" />")]
    public void Date_ValuesTheRuntimeAccepts_Build_AndRun(string markup) =>
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", Answer(markup, ""));

    [Fact]
    public void Date_MoreDaysThanTheRuntimeAccepts_IsABuildError_AndRefusedByTheRuntime()
    {
        AssertRejectedAt("<date name=\"n\" default=\"today+100001\" />", "default");

        var (renderer, input) = Terminal("");
        Assert.Throws<InvalidOperationException>(() => new DateElement { Name = "n", Default = "today+100001" }.Read(renderer, input));
    }

    // ------------------------------------------------------------------ <box>

    [Theory]
    [InlineData("<box width=\"0\"><text>x</text></box>", "width")]
    [InlineData("<box width=\"2\"><text>x</text></box>", "width")]
    [InlineData("<box width=\"0%\"><text>x</text></box>", "width")]
    [InlineData("<box border=\"none\" width=\"0\"><text>x</text></box>", "width")]
    [InlineData("<box max-width=\"2\"><text>x</text></box>", "max-width")]
    public void Box_WidthNarrowerThanABoxCanBe_IsABuildError(string markup, string attribute) =>
        AssertRejectedAt(markup, attribute);

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    public void Box_WidthNarrowerThanABoxCanBe_IsNotHonouredByTheRuntime(int width, bool bordered)
    {
        var box = new BoxElement { Width = width, Border = bordered ? BorderStyle.Single : BorderStyle.None };
        box.Add(new TextElement());

        Assert.NotEqual(width, box.Layout(40).Max(line => line.Width));
    }

    [Theory]
    [InlineData("<box width=\"3\"><text>x</text></box>", 3)]
    [InlineData("<box border=\"none\" width=\"1\"><text>x</text></box>", 1)]
    [InlineData("<box max-width=\"3\"><text>x</text></box>", 3)]
    [InlineData("<box width=\"0\" min-width=\"5\"><text>x</text></box>", 5)]
    public void Box_SmallestWidth_Builds_AndIsDrawnThatWide(string markup, int expected)
    {
        var lines = ViewHarness.Render(markup + Quit).TrimEnd('\n').Split('\n');
        Assert.Equal(expected, lines.Max(line => line.Length));
    }

    [Fact]
    public void Box_WidthIsNotChecked_WhenAThemeClassMayRemoveTheBorder() =>
        Assert.Empty(ViewHarness.Generate("<box class=\"plain\" width=\"1\"><text>x</text></box>" + Quit).Errors);
}
