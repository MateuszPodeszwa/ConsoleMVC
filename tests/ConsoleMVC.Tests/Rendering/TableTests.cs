using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Layout tests for <see cref="TableElement"/>, <see cref="TableRowElement"/> and <see cref="TableCellElement"/>.
/// </summary>
public class TableTests
{
    private static string[] Plain(IEnumerable<RenderedLine> lines) => lines.Select(l => l.ToPlainText()).ToArray();

    private static TableRowElement Row(bool header, params string[] cells)
    {
        var row = new TableRowElement { IsHeader = header };
        foreach (var content in cells)
        {
            var cell = new TableCellElement();
            cell.AppendText(content);
            row.Add(cell);
        }

        return row;
    }

    private static TableElement Table(params TableRowElement[] rows)
    {
        var table = new TableElement();
        foreach (var row in rows)
            table.Add(row);
        return table;
    }

    [Fact]
    public void Table_SizesColumnsToContent_WithHeaderSeparator()
    {
        var table = Table(Row(true, "Name", "Age"), Row(false, "Ada", "36"), Row(false, "Linus", "54"));

        Assert.Equal(
        [
            "┌───────┬─────┐",
            "│ Name  │ Age │",
            "├───────┼─────┤",
            "│ Ada   │ 36  │",
            "│ Linus │ 54  │",
            "└───────┴─────┘"
        ], Plain(table.Layout(80)));
    }

    [Fact]
    public void Table_TooWide_ShrinksWidestColumn_AndWraps()
    {
        var table = Table(Row(false, "aaaa bbbb", "cc"));

        Assert.Equal(
        [
            "┌─────┬────┐",
            "│ aaa │ cc │",
            "│ a   │    │",
            "│ bbb │    │",
            "│ b   │    │",
            "└─────┴────┘"
        ], Plain(table.Layout(12)));
    }

    [Fact]
    public void Table_FixedWidth_ExtendsLastColumn()
    {
        var table = Table(Row(false, "abc", "d"));
        table.Width = 14;
        table.Border = BorderStyle.Ascii;

        Assert.Equal(
        [
            "+-----+------+",
            "| abc | d    |",
            "+-----+------+"
        ], Plain(table.Layout(80)));
        Assert.All(Plain(table.Layout(80)), l => Assert.Equal(14, l.Length));
    }

    [Fact]
    public void Table_MissingCells_ArePadded_WithoutBorder()
    {
        var table = Table(Row(false, "a", "bb"), Row(false, "ccc"));
        table.Border = BorderStyle.None;

        Assert.Equal([" a    bb ", " ccc     "], Plain(table.Layout(80)));
    }

    [Fact]
    public void Table_HeaderCells_AreBold()
    {
        var table = Table(Row(true, "H"), Row(false, "b"));

        var header = table.Layout(80)[1];
        Assert.Contains(header.Spans, s => s.Text == "H" && s.Style.Decoration.HasFlag(TextDecoration.Bold));

        var body = table.Layout(80)[3];
        Assert.Contains(body.Spans, s => s.Text == "b" && !s.Style.Decoration.HasFlag(TextDecoration.Bold));
    }

    [Fact]
    public void Table_Empty_RendersNothing()
    {
        Assert.Empty(new TableElement().Layout(80));
    }

    [Fact]
    public void Table_MeasureWidth_IncludesChrome()
    {
        Assert.Equal(15, Table(Row(true, "Name", "Age"), Row(false, "Linus", "54")).MeasureWidth(80));
    }

    [Fact]
    public void Table_InsideBox_FitsInnerWidth()
    {
        var box = new BoxElement { Padding = 0 };
        box.Add(Table(Row(false, "x")));

        Assert.Equal(["┌─────┐", "│┌───┐│", "││ x ││", "│└───┘│", "└─────┘"], Plain(box.Layout(80)));
    }
}
