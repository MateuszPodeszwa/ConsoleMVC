using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for the C-View Markup 1.0 layout features: <see cref="ColumnsElement"/>, <see cref="Theme"/> and minimum and
/// maximum widths (docs/design/c-view-markup-1.0.md).
/// </summary>
public class MarkupV1Tests
{
    private static string[] Plain(IEnumerable<RenderedLine> lines) => lines.Select(l => l.ToPlainText()).ToArray();

    private static TextElement Text(string content)
    {
        var text = new TextElement();
        text.AppendText(content);
        return text;
    }

    private static ColumnElement Column(Length width, params Element[] children)
    {
        var column = new ColumnElement { Width = width };
        foreach (var child in children)
            column.Add(child);
        return column;
    }

    private static ColumnsElement Columns(int gap, params ColumnElement[] columns)
    {
        var element = new ColumnsElement { Gap = gap };
        foreach (var column in columns)
            element.Add(column);
        return element;
    }

    // --- columns ---

    [Fact]
    public void Columns_PlaceAutoColumnsSideBySide_AtTheirNaturalWidth()
    {
        var columns = Columns(2, Column(Length.Auto, Text("Left"), Text("two")), Column(Length.Auto, Text("Right")));

        Assert.Equal(["Left  Right", "two"], Plain(columns.Layout(40)).Select(l => l.TrimEnd()).ToArray());
        Assert.Equal(11, columns.MeasureWidth(40));
    }

    [Fact]
    public void Columns_FixedAndPercentWidths()
    {
        var columns = Columns(1, Column(Length.Fixed(5), Text("abc")), Column(Length.Percent(50), Text("xyz")));

        var lines = Plain(columns.Layout(21));

        Assert.Equal("abc   xyz", lines[0].TrimEnd()); // 5 columns + 1 gap, then 50% of the 20 left
        Assert.True(columns.HasRelativeWidth);
    }

    [Fact]
    public void Columns_WrapWhenTheNaturalWidthsDoNotFit()
    {
        var columns = Columns(1, Column(Length.Auto, Text("aaaa bbbb")), Column(Length.Auto, Text("cccc dddd")));

        Assert.Equal(["aaaa cccc", "bbbb dddd"], Plain(columns.Layout(9)));
    }

    [Fact]
    public void Columns_ANarrowColumnKeepsItsWidth_TheWideOneTakesTheRest()
    {
        var columns = Columns(1, Column(Length.Auto, Text("ab")), Column(Length.Auto, Text("one two three four")));

        Assert.Equal(["ab one two", "   three", "   four"], Plain(columns.Layout(10)).Select(l => l.TrimEnd()).ToArray());
    }

    [Fact]
    public void Columns_StackBelowTheGivenWidth()
    {
        var columns = Columns(2, Column(Length.Auto, Text("Left")), Column(Length.Auto, Text("Right")));
        columns.StackBelow = 30;

        Assert.Equal(["Left", "Right"], Plain(columns.Layout(20)));
        Assert.Equal(["Left  Right"], Plain(columns.Layout(30)).Select(l => l.TrimEnd()).ToArray());
    }

    [Fact]
    public void Columns_NeverExceedTheWidth()
    {
        var columns = Columns(3, Column(Length.Fixed(10), Text("x")), Column(Length.Fixed(10), Text("y")), Column(Length.Auto, Text("z")));

        Assert.All(columns.Layout(12), line => Assert.True(line.Width <= 12));
        Assert.Empty(new ColumnsElement().Layout(10));
    }

    [Fact]
    public void Columns_InsideABox()
    {
        var box = new BoxElement { Padding = 0 };
        box.Add(Columns(1, Column(Length.Auto, Text("a")), Column(Length.Auto, Text("b"))));

        Assert.Equal(["┌───┐", "│a b│", "└───┘"], Plain(box.Layout(40)));
    }

    [Fact]
    public void Columns_AcceptOnlyColumns()
    {
        Assert.Throws<ArgumentException>(() => new ColumnsElement().Add(Text("x")));
    }

    // --- min-width / max-width ---

    [Fact]
    public void Box_MinWidth_WidensASmallBox()
    {
        var box = new BoxElement { Padding = 0, MinWidth = 7 };
        box.Add(Text("a"));

        Assert.Equal("┌─────┐", Plain(box.Layout(40))[0]);
    }

    [Fact]
    public void Box_MaxWidth_CapsAPercentageBox_AndWraps()
    {
        var box = new BoxElement { Padding = 0, Width = Length.Percent(100), MaxWidth = 8 };
        box.Add(Text("one two three"));

        Assert.Equal(["┌──────┐", "│one   │", "│two   │", "│three │", "└──────┘"], Plain(box.Layout(80)));
    }

    [Fact]
    public void Limits_NeverExceedTheAvailableWidth()
    {
        var box = new BoxElement { MinWidth = 50 };
        box.Add(Text("a"));

        Assert.Equal(20, box.MeasureWidth(20));
    }

    [Fact]
    public void Table_MinAndMaxWidth()
    {
        var table = new TableElement { MinWidth = 12 };
        var row = new TableRowElement();
        var cell = new TableCellElement();
        cell.AppendText("a");
        row.Add(cell);
        table.Add(row);

        Assert.Equal(12, table.MeasureWidth(40));

        table.MinWidth = null;
        table.Width = Length.Percent(100);
        table.MaxWidth = 9;
        Assert.Equal(9, table.MeasureWidth(40));
    }

    // --- themes ---

    [Fact]
    public void Theme_AppliesStylesByClassName_CaseInsensitively()
    {
        var theme = new Theme().Set("brand", new ThemeStyle { Foreground = Colour.Magenta, Bold = true });
        var text = new TextElement();

        theme.Apply(text, "BRAND");

        Assert.Equal(Colour.Magenta, text.Foreground);
        Assert.True(text.Bold);
    }

    [Fact]
    public void Theme_AppliesSeveralClassesInOrder_AndIgnoresUnknownOnes()
    {
        var theme = new Theme()
            .Set("a", new ThemeStyle { Foreground = Colour.Red, Italic = true })
            .Set("b", new ThemeStyle { Foreground = Colour.Green });
        var text = new TextElement();

        theme.Apply(text, "  a missing  b ");

        Assert.Equal(Colour.Green, text.Foreground);
        Assert.True(text.Italic);
    }

    [Fact]
    public void Theme_StylesBoxesTablesSeparatorsAndErrors()
    {
        var style = new ThemeStyle
        {
            Border = BorderStyle.Double, BorderColour = Colour.Cyan, Padding = 3, Width = Length.Percent(50),
            MinWidth = 10, MaxWidth = 60, TitleColour = Colour.Yellow, Foreground = Colour.Blue
        };
        var theme = new Theme().Set("panel", style);
        var box = new BoxElement();
        var table = new TableElement();
        var separator = new SeparatorElement();
        var errors = new ValidationErrorsElement();

        foreach (var element in new Element[] { box, table, separator, errors })
            theme.Apply(element, "panel");

        Assert.Equal((BorderStyle.Double, Colour.Cyan, 3, Length.Percent(50), 10, 60, Colour.Yellow),
            (box.Border, box.BorderColour, box.Padding, box.Width, box.MinWidth, box.MaxWidth, box.TitleColour));
        Assert.Equal((BorderStyle.Double, Colour.Cyan, 3, 10, 60), (table.Border, table.BorderColour, table.Padding, table.MinWidth, table.MaxWidth));
        Assert.Equal(Colour.Blue, separator.Colour);
        Assert.Equal(Colour.Blue, errors.Colour);
    }

    [Fact]
    public void Theme_UnsetPropertiesLeaveTheElementAlone()
    {
        var box = new BoxElement { Padding = 2, Border = BorderStyle.Heavy };

        new Theme().Set("x", new ThemeStyle { BorderColour = Colour.Red }).Apply(box, "x");

        Assert.Equal((2, BorderStyle.Heavy, Colour.Red), (box.Padding, box.Border, box.BorderColour));
    }

    [Fact]
    public void DefaultTheme_HasTheDocumentedClasses()
    {
        var theme = Theme.CreateDefault();

        Assert.All(["title", "muted", "success", "warning", "danger", "info", "panel"], name => Assert.True(theme.Contains(name)));
    }

    [Fact]
    public void Theme_RejectsInvalidClassNames()
    {
        Assert.Throws<ArgumentException>(() => new Theme().Set("two words", new ThemeStyle()));
        Assert.Throws<ArgumentException>(() => new Theme().Set("", new ThemeStyle()));
    }
}
