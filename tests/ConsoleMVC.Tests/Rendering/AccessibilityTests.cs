using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>Tests for the screen-reader renderer and the high-contrast theme (docs/design/accessibility.md).</summary>
public class AccessibilityTests
{
    private static (ScreenReaderRenderer Renderer, StringWriter Output) Reader(int width = 60)
    {
        var output = new StringWriter { NewLine = "\n" };
        return (new ScreenReaderRenderer(new MarkupRenderer(new PlainTextRenderTarget(output), () => width)), output);
    }

    private static TextElement Text(string text)
    {
        var element = new TextElement();
        element.AppendText(text);
        return element;
    }

    private static readonly char[] BoxDrawing = "─│┌┐└┘├┤┬┴┼╭╮╯╰═║╔╗╚╝━┃█░▓▁▂▃▄▅▆▇⠋".ToCharArray();

    [Fact]
    public void Boxes_LoseTheirBorders_AndKeepTheirTitle()
    {
        var (renderer, output) = Reader();
        var box = new BoxElement { Title = "Account" };
        box.Add(Text("Balance: 42"));

        renderer.Render(box);

        Assert.Equal("Account\nBalance: 42\n", output.ToString());
    }

    [Fact]
    public void Tables_ReadEachRowWithItsHeaders()
    {
        var (renderer, output) = Reader();
        var table = new TableElement();
        foreach (var (cells, header) in new[] { (new[] { "Name", "Age" }, true), (new[] { "Ada", "36" }, false), (new[] { "Alan", "41" }, false) })
        {
            var row = new TableRowElement { IsHeader = header };
            foreach (var value in cells)
            {
                var cell = new TableCellElement();
                cell.AppendText(value);
                row.Add(cell);
            }

            table.Add(row);
        }

        renderer.Render(table);

        Assert.Equal("Name: Ada, Age: 36\nName: Alan, Age: 41\n", output.ToString());
    }

    [Fact]
    public void Columns_AreReadOneAfterAnother_AndSeparatorsAreSilent()
    {
        var (renderer, output) = Reader();
        var columns = new ColumnsElement();
        foreach (var text in new[] { "Left", "Right" })
        {
            var column = new ColumnElement();
            column.Add(Text(text));
            columns.Add(column);
        }

        renderer.Render(columns);
        renderer.Render(new SeparatorElement());

        Assert.Equal("Left\nRight\n", output.ToString());
    }

    [Fact]
    public void LiveElementsAndCharts_AreDescribedInWords()
    {
        var (renderer, output) = Reader();
        var chart = new BarChartElement();
        chart.Add(new BarElement { Label = "Mon", Value = 12 });
        chart.Add(new BarElement { Label = "Tue", Value = 7.5 });

        renderer.Render(new ProgressElement { Label = "Import ", Value = 3, Max = 4 });
        renderer.Render(new SpinnerElement { Label = "Connecting" });
        renderer.Render(new SpinnerElement { Label = "Connected", Done = true });
        renderer.Render(chart);
        renderer.Render(new SparklineElement { Label = "Load ", Values = [3, 9, 1, 4] });
        renderer.Render(new ShortcutsElement { Items = [new ShortcutHint("F1", "Help"), new ShortcutHint("Esc", "Back")] });
        renderer.Render(new BreadcrumbsElement { Items = ["Home", "Orders"] });

        Assert.Equal(
            "Import: 75%\nConnecting\nConnected: done\nMon: 12\nTue: 7.5\nLoad: 4 values from 1 to 9, latest 4\n" +
            "Shortcuts: F1 Help, Esc Back\nYou are here: Home, Orders\n", output.ToString());
    }

    [Fact]
    public void Markdown_HasNoRulesOrBars()
    {
        var (renderer, output) = Reader();

        renderer.Render(new MarkdownElement { Text = "# Notes\n\n> quoted\n\n---\n\nEnd" });

        Assert.Equal("Notes\nQuote: quoted\n\nEnd\n", output.ToString());
    }

    [Fact]
    public void NothingIsDrawnWithBoxCharacters_OrRewritten()
    {
        var (renderer, output) = Reader();
        var box = new BoxElement { Title = "T" };
        box.Add(new ProgressElement { Value = 50 });
        var scroll = new ScrollElement { Height = 1 };
        scroll.Add(Text("a"));
        scroll.Add(Text("b"));

        renderer.Render(box);
        scroll.Show(renderer, new ScriptedKeys(ConsoleKey.DownArrow));

        Assert.False(renderer.CanRewrite);
        Assert.False(renderer.SetFullScreen(true));
        Assert.Equal(-1, output.ToString().IndexOfAny(BoxDrawing));
        Assert.EndsWith("a\nb\n", output.ToString());
    }

    [Fact]
    public void Clear_SeparatesScreensWithABlankLine_InsteadOfErasingThem()
    {
        var (renderer, output) = Reader();

        renderer.Render(Text("first"));
        renderer.Clear();
        renderer.Render(Text("second"));

        Assert.Equal("first\n\nsecond\n", output.ToString());
    }

    [Fact]
    public void HighContrastTheme_DefinesTheBuiltInClasses_InBoldBrightColours()
    {
        var theme = Theme.CreateHighContrast();

        foreach (var name in new[] { "title", "muted", "success", "warning", "danger", "info", "panel" })
            Assert.True(theme.Contains(name), name);

        var muted = new TextElement();
        theme.Apply(muted, "muted");
        Assert.False(muted.Dim); // dim text is hard to read
        Assert.Equal(Colour.White, muted.Foreground);
    }
}
