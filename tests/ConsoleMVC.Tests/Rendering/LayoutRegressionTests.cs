using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Regression tests for layout bugs found in review: lines wider than their width, double-applied percentages,
/// overflow in <see cref="Length.Resolve"/> and word-wrapping at tiny widths.
/// </summary>
public class LayoutRegressionTests
{
    private static TextElement Text(string content)
    {
        var text = new TextElement();
        text.AppendText(content);
        return text;
    }

    private static BoxElement Box(Element child, Action<BoxElement>? configure = null)
    {
        var box = new BoxElement();
        configure?.Invoke(box);
        box.Add(child);
        return box;
    }

    /// <summary>Elements covering every layout path, used by the width invariant below.</summary>
    public static TheoryData<string> Samples => new()
    {
        "text", "cjk", "box", "padded", "nested", "untitled", "separator", "notitle", "table", "cjktable", "menu", "longkey"
    };

    private static TableElement Table(params string[][] rows)
    {
        var table = new TableElement();
        foreach (var cells in rows)
        {
            var row = new TableRowElement();
            foreach (var content in cells)
            {
                var cell = new TableCellElement();
                cell.AppendText(content);
                row.Add(cell);
            }

            table.Add(row);
        }

        return table;
    }

    private static MenuElement Menu(string key, string label)
    {
        var option = new MenuOptionElement { Key = key, Target = "A" };
        option.AppendText(label);
        var menu = new MenuElement();
        menu.Add(option);
        return menu;
    }

    private static Element Sample(string name) => name switch
    {
        "text" => Text("The quick brown fox jumps over the lazy dog"),
        "cjk" => Text("日本語のテキスト 🙂 emoji"),
        "box" => Box(Text("Hello world"), b => b.Title = "Greeting"),
        "padded" => Box(Text("Hello"), b => { b.Padding = 20; b.Width = 30; }),
        "nested" => Box(Box(Text("Inner 日本"), b => b.Width = "50%"), b => b.Title = "Outer"),
        "untitled" => Box(Text("x"), b => b.Border = BorderStyle.Double),
        "separator" => new SeparatorElement { Width = 12 },
        "table" => Table(["Name", "Age"], ["Ada Lovelace", "36"]),
        "cjktable" => Table(["日本語日本語", "x"]),
        "menu" => Menu("1", "A fairly long menu label"),
        "longkey" => Menu("verylongkey", "Label"),
        _ => Box(Text("item"), b => { b.Border = BorderStyle.None; b.Title = "A long heading"; })
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void NoLine_IsEverWiderThanTheWidthGiven(string sample)
    {
        var element = Sample(sample);

        for (var width = 1; width <= 40; width++)
        {
            foreach (var line in element.Layout(width))
                Assert.True(line.Width <= width, $"{sample} at width {width}: '{line.ToPlainText()}' is {line.Width} wide");
        }
    }

    [Fact]
    public void Box_PaddingLargerThanItsWidth_IsReducedToFit()
    {
        var lines = Box(Text("Hello"), b => { b.Width = 30; b.Padding = 20; }).Layout(80);

        Assert.All(lines, l => Assert.Equal(30, l.Width));
        Assert.Contains(lines, l => l.ToPlainText().Contains("Hello"));
    }

    [Fact]
    public void PercentageChild_InsideAutoBox_IsSizedOnce()
    {
        var outer = Box(Box(Text("Inner"), b => b.Width = "50%"), b => b.Padding = 1);

        var lines = outer.Layout(80).Select(l => l.ToPlainText()).ToArray();

        // The outer box fills the width, so the inner box gets 50% of the outer box's 76 inner columns
        Assert.Equal(80, lines[0].Length);
        Assert.Equal('┌', lines[1][2]);
        Assert.Equal('┐', lines[1][2 + 37]);
    }

    [Fact]
    public void Length_Percent_DoesNotOverflow()
    {
        Assert.Equal(25_000_000, Length.Percent(50).Resolve(50_000_000, 0));
        Assert.Equal(int.MaxValue / 2, Length.Percent(50).Resolve(int.MaxValue, 0));
    }

    [Fact]
    public void Wrap_WideCharactersAtWidthOne_ProducesNoTrailingEmptyLine()
    {
        Assert.Equal(["日", "本"], TextWrapper.Wrap("日本", 1));
    }

    [Fact]
    public void Wrap_IndentNeverLeavesASpacesOnlyLine()
    {
        Assert.Equal(["   日", "本語"], TextWrapper.Wrap("    日本語", 5));
    }

    [Fact]
    public void Text_TrailingSpaces_DoNotWidenAnAutoBox()
    {
        var text = new TextElement();
        text.AppendValue("abc      ");

        Assert.Equal(3, text.MeasureWidth(80));
        Assert.Equal("│ abc │", Box(text).Layout(80)[1].ToPlainText());
    }

    [Fact]
    public void RenderedLine_Clip_CutsSpansAtColumnBoundaries()
    {
        var line = new RenderedLine([new Span("ab", TextStyle.Plain), new Span("日本", TextStyle.Plain)]);

        Assert.Equal("ab日", line.Clip(5).ToPlainText());
        Assert.Same(line, line.Clip(6));
    }

    [Fact]
    public void PercentageTable_InsideAutoBox_IsSizedOnce()
    {
        var table = Table(["x"]);
        table.Width = "50%";
        var outer = Box(table);

        Assert.Equal(80, outer.Layout(80)[0].Width);
        Assert.Equal(38, outer.Layout(80)[1].ToPlainText().Trim('│', ' ').Length);
    }

    [Fact]
    public void Table_FixedWidthSmallerThanItsGrid_StaysWithinWidth()
    {
        var table = Table(["a", "b"]);
        table.Width = 5;

        Assert.All(table.Layout(80), l => Assert.True(l.Width <= 5, l.ToPlainText()));
    }

    [Fact]
    public void Table_HeaderCells_KeepTheirOwnDecoration()
    {
        var table = new TableElement();
        var header = new TableRowElement { IsHeader = true };
        var cell = new TableCellElement { Dim = true };
        cell.AppendText("H");
        header.Add(cell);
        table.Add(header);

        var span = table.Layout(80)[1].Spans.Single(s => s.Text == "H");
        Assert.Equal(TextDecoration.Dim, span.Style.Decoration);
    }
}
