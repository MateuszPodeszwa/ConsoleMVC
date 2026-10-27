using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for <c>&lt;bar-chart&gt;</c>, <c>&lt;bar&gt;</c> and <c>&lt;sparkline&gt;</c> (docs/design/charts.md).</summary>
public class ChartMarkupTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    [Fact]
    public void BarChart_DrawsBarsFromALoop()
    {
        const string view = """
            @model System.Collections.Generic.List<(string Day, int Sales)>
            <bar-chart style="ascii" color="cyan" label-colour="grey">
                @foreach (var (day, sales) in Model)
                {
                    <bar label="@day" value="@sales" />
                }
                <bar label="Sun" value="0" colour="red" />
            </bar-chart>
            """;

        var output = ViewHarness.Render(view + Quit, "new System.Collections.Generic.List<(string, int)> { (\"Mon\", 10), (\"Tue\", 5) }", width: 20);

        Assert.Equal("Mon ############# 10\nTue #######        5\nSun                0\n", output);
    }

    [Fact]
    public void BarChart_TakesMaxFormatAndHidesValues()
    {
        var output = ViewHarness.Render(
            "<bar-chart max=\"20\" values=\"false\" style=\"ascii\"><bar label=\"a\" value=\"10\" /></bar-chart>" + Quit, width: 12);

        Assert.Equal("a #####     \n", output);
    }

    [Fact]
    public void Sparkline_TakesALiteralList_OrAnyNumberSequence()
    {
        var output = ViewHarness.Render(
            "@model int[]\n<sparkline values=\"0 1 2, 3 4 5 6 7\" />\n<sparkline label=\"Q \" values=\"@Model\" show-last />" + Quit,
            "new[] { 0, 7 }");

        Assert.Equal("▁▂▃▄▅▆▇█\nQ ▁█ 7\n", output);
    }

    [Theory]
    [InlineData("<bar label=\"x\" value=\"1\" />")]
    [InlineData("<bar-chart><text>x</text></bar-chart>")]
    [InlineData("<bar-chart><bar label=\"x\" /></bar-chart>")]
    [InlineData("<sparkline />")]
    [InlineData("<sparkline values=\"1, two\" />")]
    [InlineData("<bar-chart style=\"round\"><bar value=\"1\" /></bar-chart>")]
    public void Charts_RejectInvalidMarkup(string markup)
    {
        var result = ViewHarness.Generate(markup + Quit);

        Assert.NotEmpty(result.GeneratorDiagnostics);
    }
}
