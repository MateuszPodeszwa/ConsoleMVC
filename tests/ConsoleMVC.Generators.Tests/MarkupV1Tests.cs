using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Generator tests for C-View Markup 1.0: <c>&lt;columns&gt;</c>, <c>class</c> and <c>min-width</c> / <c>max-width</c>
/// (docs/design/c-view-markup-1.0.md).
/// </summary>
public class MarkupV1Tests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    [Fact]
    public void Columns_RenderSideBySide()
    {
        var output = ViewHarness.Render(
            "<columns gap=\"3\">\n<column width=\"6\"><text>Menu</text><text>Help</text></column>\n<column><text>Main</text></column>\n</columns>" + Quit);

        Assert.Equal("Menu     Main\nHelp\n", string.Join("\n", output.Split('\n').Select(l => l.TrimEnd())));
    }

    [Fact]
    public void Columns_StackBelowAWidth()
    {
        var output = ViewHarness.Render(
            "<columns stack-below=\"60\"><column><text>A</text></column><column><text>B</text></column></columns>" + Quit, width: 40);

        Assert.Equal("A\nB\n", output);
    }

    [Fact]
    public void Columns_CanBeGeneratedInALoop_AndNestedInABox()
    {
        var output = ViewHarness.Render(
            "@model string[]\n<box padding=\"0\"><columns gap=\"1\">@foreach (var s in Model) { <column><text>@s</text></column> }</columns></box>" + Quit,
            "new[] { \"a\", \"b\", \"c\" }");

        Assert.StartsWith("┌─────┐\n│a b c│\n", output);
    }

    [Fact]
    public void ColumnOutsideColumns_ReportsCVW103()
    {
        var result = ViewHarness.Generate("<column><text>x</text></column>" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW103");
    }

    [Fact]
    public void TextDirectlyInColumns_ReportsCVW110()
    {
        var result = ViewHarness.Generate("<columns>oops</columns>" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW110");
    }

    [Fact]
    public void Class_AppliesTheThemeBeforeTheElementsOwnAttributes()
    {
        // The default theme's "panel" is a rounded box with padding 2; padding="0" overrides it
        var output = ViewHarness.Render("<box class=\"panel\" padding=\"0\"><text>x</text></box>" + Quit);

        Assert.StartsWith("╭─╮\n│x│\n╰─╯", output);
    }

    [Fact]
    public void Class_AcceptsExpressionsAndSeveralNames()
    {
        var result = ViewHarness.Generate(
            "var kind = \"danger\";\n<text class=\"title @kind\">x</text>\n<separator class=\"muted\" />\n<errors class=\"warning\" />\n<table class=\"panel\"><row><cell class=\"info\">a</cell></row></table>" + Quit);

        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        Assert.Contains("this.Theme.Apply(", result.GeneratedSource);
    }

    [Fact]
    public void MinAndMaxWidth_OnBoxes()
    {
        var output = ViewHarness.Render("<box min-width=\"9\" padding=\"0\"><text>a</text></box>\n<box width=\"100%\" max-width=\"5\" padding=\"0\"><text>b</text></box>" + Quit, width: 40);

        Assert.StartsWith("┌───────┐", output);
        Assert.Contains("\n┌───┐\n", output);
    }

    [Fact]
    public void InvalidWidthLimit_ReportsCVW101()
    {
        var result = ViewHarness.Generate("<table max-width=\"wide\"><row><cell>a</cell></row></table>" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW101");
    }

    [Fact]
    public void FillColumns_InMarkup()
    {
        var output = ViewHarness.Render(
            "<columns gap=\"1\"><column width=\"*\"><text>left</text></column><column><text>right</text></column></columns>" + Quit, width: 20);

        Assert.Equal("left           right\n", output); // the fill column is 20 - 1 - 5 = 14 wide
    }
}
