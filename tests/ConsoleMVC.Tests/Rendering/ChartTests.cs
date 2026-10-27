using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>Tests for <c>&lt;bar-chart&gt;</c> and <c>&lt;sparkline&gt;</c> (docs/design/charts.md).</summary>
public class ChartTests
{
    private static string[] Lines(Element element, int width = 40) =>
        element.Layout(width).Select(l => l.ToPlainText()).ToArray();

    private static BarChartElement Chart(params (string Label, double Value)[] bars)
    {
        var chart = new BarChartElement();
        foreach (var (label, value) in bars)
            chart.Add(new BarElement { Label = label, Value = value });
        return chart;
    }

    // ------------------------------------------------------------------ <bar-chart>

    [Fact]
    public void BarChart_ScalesBarsToTheLargestValue_AndAlignsLabelsAndValues()
    {
        var chart = Chart(("Mon", 5), ("Tue", 10), ("Wednesday", 2.5));
        chart.Style = BarChartStyle.Ascii;

        var lines = Lines(chart, 30);

        // label 9 + space + area 16 + space + value 3 = 30
        Assert.Equal("Mon       " + new string('#', 8) + new string(' ', 8) + "   5", lines[0]);
        Assert.Equal("Tue       " + new string('#', 16) + "  10", lines[1]);
        Assert.Equal("Wednesday " + new string('#', 4) + new string(' ', 12) + " 2.5", lines[2]);
    }

    [Fact]
    public void BarChart_DrawsEighthsOfACell_WithTheBlockStyle()
    {
        var chart = Chart(("a", 1), ("b", 8));
        chart.ShowValues = false;

        var lines = Lines(chart, 10); // area 8: "a" is one cell, "b" eight

        Assert.Equal("a █       ", lines[0]);
        Assert.Equal("b ████████", lines[1]);

        chart.Bars[0].Value = 0.5;
        Assert.Equal("a ▌       ", Lines(chart, 10)[0]);
    }

    [Fact]
    public void BarChart_UsesMax_ClampsNegatives_AndFormatsValues()
    {
        var chart = Chart(("x", 1234.5), ("y", -3));
        chart.Max = 2469;
        chart.Format = "N0";
        chart.Style = BarChartStyle.Ascii;

        var lines = Lines(chart, 22);

        Assert.StartsWith("x ######", lines[0]);
        Assert.DoesNotContain('#', lines[1]);
        Assert.EndsWith(" -3", lines[1]);
        Assert.EndsWith(1234.5.ToString("N0") , lines[0]);
    }

    [Fact]
    public void BarChart_IsEmpty_WithoutBars_AndDrawsEmptyBars_WhenNothingIsPositive()
    {
        Assert.Empty(Lines(new BarChartElement()));

        var zero = Chart(("a", 0), ("b", double.NaN));
        Assert.All(Lines(zero), line => Assert.DoesNotContain('█', line));
    }

    [Fact]
    public void BarChart_ShortensLongLabels_AndSanitisesThem()
    {
        var chart = Chart(("A label far too long for the chart\u001b[2J", 1));

        var line = Lines(chart, 30)[0];

        Assert.Equal(30, line.Length);
        Assert.DoesNotContain('\u001b', line);
        Assert.Contains('…', line);
        Assert.Contains('█', line);
    }

    [Fact]
    public void BarChart_GivesEachBarItsOwnColour()
    {
        var chart = Chart(("a", 1));
        chart.Bars[0].Colour = Colour.FromConsoleColor(ConsoleColor.Red);

        var bar = chart.Layout(20)[0].Spans.Single(s => s.Text.Contains('█'));

        Assert.Equal(Colour.FromConsoleColor(ConsoleColor.Red), bar.Style.Foreground);
    }

    // ------------------------------------------------------------------ <sparkline>

    [Fact]
    public void Sparkline_MapsValuesToEightLevels()
    {
        var line = new SparklineElement { Values = [0, 1, 2, 3, 4, 5, 6, 7] };

        Assert.Equal("▁▂▃▄▅▆▇█", Lines(line)[0]);
        Assert.Equal(8, line.MeasureWidth(40));
    }

    [Fact]
    public void Sparkline_ShowsTheMostRecentValues_WhenThereIsNoRoom()
    {
        var line = new SparklineElement { Label = "Q ", Values = [7, 7, 0, 7], ShowLast = true };

        Assert.Equal("Q ▁█ 7", Lines(line, 6)[0]);
        Assert.Equal("Q █▁█ 7", Lines(line, 7)[0]); // 3 cells: the last three values, scaled on what is shown
    }

    [Fact]
    public void Sparkline_UsesMinAndMax_AndHandlesFlatAndMissingValues()
    {
        Assert.Equal("▄▄▄", Lines(new SparklineElement { Values = [2, 2, 2] })[0]);
        Assert.Equal("▁ █", Lines(new SparklineElement { Values = [0, double.NaN, 9] })[0]);
        Assert.Equal("▁▁█", Lines(new SparklineElement { Values = [-5, 0, 50], Min = 0, Max = 10 })[0]);
        Assert.Empty(Lines(new SparklineElement()));
    }

    [Fact]
    public void ChartValues_AcceptAnyNumberType()
    {
        Assert.Equal([1d, 2d], ChartValues.From(new List<int> { 1, 2 }));
        Assert.Equal([1.5d], ChartValues.From([1.5m]));
        Assert.Equal([3d], ChartValues.From(new long[] { 3 }));
        Assert.Equal([0.5d], ChartValues.From(new[] { 0.5f }));
        Assert.Empty(ChartValues.From((IEnumerable<double>?)null));
    }
}
