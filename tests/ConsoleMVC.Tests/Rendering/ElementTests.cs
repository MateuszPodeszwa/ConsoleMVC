using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Layout tests for the markup elements. Each test inspects the plain text of the
/// produced <see cref="RenderedLine"/>s, so they are independent of colour output.
/// </summary>
public class ElementTests
{
    private static string[] Plain(IEnumerable<RenderedLine> lines) =>
        lines.Select(l => l.ToPlainText()).ToArray();

    private static TextElement Text(string content, Alignment align = Alignment.Left)
    {
        var text = new TextElement { Align = align };
        text.AppendText(content);
        return text;
    }

    // --- TextElement ---

    [Fact]
    public void Text_Left_DoesNotPad()
    {
        Assert.Equal(["Hello"], Plain(Text("Hello").Layout(20)));
    }

    [Fact]
    public void Text_Centre_PadsLeft()
    {
        Assert.Equal(["   Hi"], Plain(Text("Hi", Alignment.Centre).Layout(8)));
    }

    [Fact]
    public void Text_Right_PadsLeft()
    {
        Assert.Equal(["      Hi"], Plain(Text("Hi", Alignment.Right).Layout(8)));
    }

    [Fact]
    public void Text_AppendValue_FormatsAndSanitises()
    {
        var text = new TextElement();
        text.AppendText("Count: ");
        text.AppendValue(42);
        text.AppendText(" ");
        text.AppendValue("\u001b[31mred");
        text.AppendValue(null);

        Assert.Equal(["Count: 42 [31mred"], Plain(text.Layout(80)));
    }

    [Fact]
    public void Text_NewlinesInValues_BecomeLineBreaks()
    {
        var text = new TextElement();
        text.AppendValue("one\ntwo");
        text.AppendLineBreak();
        text.AppendText("three");

        Assert.Equal(["one", "two", "three"], Plain(text.Layout(80)));
    }

    [Fact]
    public void Text_Wraps_WhenWrapEnabled()
    {
        Assert.Equal(["aaa bbb", "ccc"], Plain(Text("aaa bbb ccc").Layout(7)));
    }

    [Fact]
    public void Text_Truncates_WhenWrapDisabled()
    {
        var text = Text("aaa bbb ccc");
        text.Wrap = false;

        Assert.Equal(["aaa bb…"], Plain(text.Layout(7)));
    }

    [Fact]
    public void Text_MeasureWidth_IsLongestLine()
    {
        Assert.Equal(5, Text("ab\nabcde\nabc").MeasureWidth(80));
        Assert.Equal(3, Text("abcde").MeasureWidth(3));
    }

    [Fact]
    public void Text_Style_AppliedToSpans()
    {
        var text = Text("x");
        text.Foreground = Colour.Red;
        text.Bold = true;

        var span = Assert.Single(text.Layout(10)[0].Spans);
        Assert.Equal(Colour.Red, span.Style.Foreground);
        Assert.True(span.Style.Decoration.HasFlag(TextDecoration.Bold));
    }

    // --- BoxElement ---

    [Fact]
    public void Box_AutoWidth_FitsContentAndTitle()
    {
        var box = new BoxElement { Title = "Hi" };
        box.Add(Text("Hello"));

        Assert.Equal(
        [
            "┌─ Hi ──┐",
            "│ Hello │",
            "└───────┘"
        ], Plain(box.Layout(80)));
    }

    [Fact]
    public void Box_LongTitle_WidensBox()
    {
        var box = new BoxElement { Title = "Dashboard" };
        box.Add(Text("x"));

        Assert.Equal(
        [
            "┌─ Dashboard ─┐",
            "│ x           │",
            "└─────────────┘"
        ], Plain(box.Layout(80)));
    }

    [Fact]
    public void Box_FixedWidth_DoubleBorder_NoPadding()
    {
        var box = new BoxElement { Width = 10, Border = BorderStyle.Double, Padding = 0 };
        box.Add(Text("ab"));

        Assert.Equal(
        [
            "╔════════╗",
            "║ab      ║",
            "╚════════╝"
        ], Plain(box.Layout(80)));
    }

    [Fact]
    public void Box_WidthLargerThanAvailable_IsCapped_AndContentWraps()
    {
        var box = new BoxElement { Width = 100 };
        box.Add(Text("aaa bbb"));

        Assert.Equal(
        [
            "┌───────┐",
            "│ aaa   │",
            "│ bbb   │",
            "└───────┘"
        ], Plain(box.Layout(9)));
    }

    [Fact]
    public void Box_Nested_RendersInsideParent()
    {
        var inner = new BoxElement { Border = BorderStyle.Rounded, Padding = 0 };
        inner.Add(Text("in"));
        var outer = new BoxElement { Border = BorderStyle.Ascii, Padding = 0 };
        outer.Add(inner);

        Assert.Equal(
        [
            "+----+",
            "|╭──╮|",
            "|│in│|",
            "|╰──╯|",
            "+----+"
        ], Plain(outer.Layout(80)));
    }

    [Fact]
    public void Box_NoBorder_ShowsTitleAsHeading()
    {
        var box = new BoxElement { Title = "Menu", Border = BorderStyle.None, Padding = 0 };
        box.Add(Text("item"));

        Assert.Equal(["Menu", "item"], Plain(box.Layout(80)));
    }

    [Fact]
    public void Box_TitleTooLong_IsTruncated()
    {
        var box = new BoxElement { Title = "A very long title", Width = 12 };
        box.Add(Text("x"));

        Assert.Equal("┌─ A very… ┐", Plain(box.Layout(80))[0]);
    }

    [Fact]
    public void Box_Empty_RendersFrameOnly()
    {
        Assert.Equal(["┌──┐", "└──┘"], Plain(new BoxElement().Layout(80)));
    }

    [Fact]
    public void Box_SeparatorInside_FillsInnerWidth()
    {
        var box = new BoxElement();
        box.Add(Text("abcd"));
        box.Add(new SeparatorElement());

        Assert.Equal(
        [
            "┌──────┐",
            "│ abcd │",
            "│ ──── │",
            "└──────┘"
        ], Plain(box.Layout(80)));
    }

    [Fact]
    public void Box_BorderColour_AppliedToBorderSpans()
    {
        var box = new BoxElement { BorderColour = Colour.Cyan };
        box.Add(Text("x"));

        var top = box.Layout(80)[0];
        Assert.All(top.Spans, s => Assert.Equal(Colour.Cyan, s.Style.Foreground));
    }

    // --- SeparatorElement / LineBreakElement ---

    [Fact]
    public void Separator_FillsAvailableWidth()
    {
        Assert.Equal(["═════"], Plain(new SeparatorElement { Style = LineStyle.Double }.Layout(5)));
    }

    [Fact]
    public void Separator_FixedWidth()
    {
        Assert.Equal(["---"], Plain(new SeparatorElement { Style = LineStyle.Ascii, Width = 3 }.Layout(10)));
    }

    [Fact]
    public void Separator_MeasuresAsZero_SoItNeverWidensAutoBoxes()
    {
        Assert.Equal(0, new SeparatorElement().MeasureWidth(80));
    }

    [Fact]
    public void LineBreak_ProducesEmptyLines()
    {
        Assert.Equal(["", ""], Plain(new LineBreakElement { Count = 2 }.Layout(10)));
    }
}
