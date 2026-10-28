namespace ConsoleMVC.Preview.Tests;

/// <summary>
/// Tests of <see cref="CvwPreview"/> and <see cref="PreviewBuilder"/>: what a view looks like without running the
/// application. See docs/design/editor-support.md, section 4.
/// </summary>
public class PreviewTests
{
    private static readonly InMemoryViewFiles NoFiles = new([]);

    /// <summary>Draws <paramref name="view"/> as plain text, with no shared views.</summary>
    private static string Text(string view, int width = 40) =>
        CvwPreview.Render(view, NoFiles, new PreviewOptions(Width: width)).Output;

    // Text output ends lines as the system does (\r\n on Windows), as a terminal expects
    private static string[] Lines(string output) => output.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');

    [Fact]
    public void Text_IsDrawn()
    {
        Assert.Equal("Hello, world", Lines(Text("<text>Hello, world</text>"))[0].TrimEnd());
    }

    [Fact]
    public void Expressions_BecomePlaceholders()
    {
        Assert.Contains("Hi {Model.Name}!", Text("<text>Hi @Model.Name!</text>"));
    }

    [Fact]
    public void ExplicitExpressions_BecomePlaceholders()
    {
        Assert.Contains("Total: {Model.Price * 2}", Text("<text>Total: @(Model.Price * 2)</text>"));
    }

    [Fact]
    public void AnAttributeWithAnExpression_IsAPlaceholder()
    {
        Assert.Contains("{Model.Title}", Text("<box title=\"@Model.Title\"><text>x</text></box>"));
    }

    [Fact]
    public void AnAttributeMixingTextAndExpressions_KeepsTheText()
    {
        Assert.Contains("Order {Model.Id}", Text("<box title=\"Order @Model.Id\"><text>x</text></box>"));
    }

    [Fact]
    public void AChoiceAttribute_SetsAPredefinedValue()
    {
        var output = Text("<box border=\"double\"><text>x</text></box>");
        Assert.StartsWith("╔", output);
    }

    [Fact]
    public void AnEnumAttribute_IsSet()
    {
        var line = Lines(Text("<text align=\"right\">x</text>", width: 20))[0];
        Assert.Equal(new string(' ', 19) + "x", line);
    }

    [Fact]
    public void AnIntegerAttribute_IsSet()
    {
        var box = Lines(Text("<box padding=\"2\"><text>x</text></box>"));
        Assert.Equal("│  x  │", box[1]);
    }

    [Fact]
    public void ALengthAttribute_IsSet()
    {
        var box = Lines(Text("<box width=\"10\"><text>x</text></box>"));
        Assert.Equal(10, box[0].Length);
    }

    [Fact]
    public void ANumberListAttribute_IsSet()
    {
        Assert.Contains("▁█", Text("<sparkline values=\"1, 8\" />"));
    }

    [Fact]
    public void AComputedNumberList_ShowsASample()
    {
        var output = Text("<sparkline values=\"@Model.Load\" min=\"0\" max=\"100\" />");
        Assert.Equal(AttributeValues.SampleSeries.Count, output.TrimEnd().Length);
    }

    [Fact]
    public void AComputedValue_ShowsASample()
    {
        Assert.Contains("30%", Text("<progress value=\"@Model.Done\" />"));
    }

    [Fact]
    public void AFlagAttribute_IsSet()
    {
        var html = CvwPreview.Render("<text bold>x</text>", NoFiles, new PreviewOptions(Format: PreviewFormat.Html)).Output;
        Assert.Contains("font-weight:bold", html);
    }

    [Fact]
    public void AThemeClass_IsApplied_AndAttributesOverrideIt()
    {
        var html = CvwPreview.Render("<text class=\"danger\" color=\"green\">x</text><text class=\"danger\">y</text>", NoFiles,
            new PreviewOptions(Format: PreviewFormat.Html)).Output;
        Assert.Contains("<span style=\"color:#00ff00;font-weight:bold\">x</span>", html);
        Assert.Contains("<span style=\"color:#ff0000;font-weight:bold\">y</span>", html);
    }

    [Fact]
    public void TextInABox_IsAnImplicitTextElement()
    {
        Assert.Contains("│ Loose text │", Text("<box>Loose text</box>"));
    }

    [Fact]
    public void LineBreaks_AreDrawn()
    {
        Assert.Equal(["a", "", "", "b"], Lines(Text("<text>a<br count=\"3\" />b</text>")).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void Code_IsSkipped_AndALoopBodyIsDrawnOnce()
    {
        var view = """
                   @{ var x = 1; }
                   @foreach (var item in Model.Items)
                   {
                       <text>@item</text>
                   }
                   @if (Model.Empty)
                   {
                       <text>None</text>
                   }
                   """;
        Assert.Equal(["{item}", "None"], Lines(Text(view)).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void Tables_HaveTheirRows()
    {
        var output = Text("""
                          <table border="ascii">
                              <row header><cell>Name</cell></row>
                              <row><cell>@user.Name</cell></row>
                          </table>
                          """);
        Assert.Contains("| Name        |", output);
        Assert.Contains("| {user.Name} |", output);
    }

    [Fact]
    public void ChartBars_ShowDifferentSamples()
    {
        var output = Lines(Text("""
                                <bar-chart>
                                    <bar label="A" value="@Model.A" />
                                    <bar label="B" value="@Model.B" />
                                </bar-chart>
                                """));
        Assert.EndsWith(" 30", output[0]);
        Assert.EndsWith(" 50", output[1]);
    }

    [Fact]
    public void AMenu_IsFollowedByItsPrompt()
    {
        var output = Lines(Text("<menu prompt=\"Pick:\"><option key=\"1\" quit>Exit</option></menu>"));
        Assert.Equal("[1] Exit", output[0].TrimEnd());
        Assert.Equal("Pick:", output[1].TrimEnd());
    }

    [Fact]
    public void ASelect_ShowsItsChoices_WithTheFirstHighlighted()
    {
        // As a terminal shows it (PreviewMatchesTheAppTests compares it with the application)
        var output = Lines(Text("<select name=\"C\" label=\"Colour\"><choice value=\"r\">Red</choice><choice value=\"b\">Blue</choice></select>"));
        Assert.Equal(["Colour (Up/Down, Enter)", " > Red", "   Blue"], output.Select(l => l.TrimEnd()));
    }

    [Fact]
    public void ASelectWithoutChoices_IsANote_NotACrash()
    {
        var result = CvwPreview.Render("<text>Before</text><select name=\"C\" label=\"Colour\"></select><text>After</text>", NoFiles,
            new PreviewOptions());
        Assert.Equal(["Before", "After"], Lines(result.Output).Select(l => l.TrimEnd()));
        Assert.Contains(result.Notes, n => n.Contains("has no choices"));
    }

    [Fact]
    public void Errors_ShowASampleMessage()
    {
        Assert.Contains("{validation message}", Text("<errors />"));
    }

    [Fact]
    public void DuplicateMenuKeys_AreANote_NotACrash()
    {
        var result = CvwPreview.Render("<menu><option key=\"1\" quit>A</option><option key=\"1\" quit>B</option></menu>", NoFiles,
            new PreviewOptions());
        Assert.Contains("[1] A", result.Output);
    }

    [Fact]
    public void APartial_IsDrawnInPlace()
    {
        var files = new InMemoryViewFiles(new() { ["Header"] = "<text>Shop</text>" });
        var output = CvwPreview.Render("<partial name=\"Header\" /><text>Body</text>", files, new PreviewOptions()).Output;
        Assert.Equal(["Shop", "Body"], Lines(output).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void AMissingPartial_IsANote()
    {
        var result = CvwPreview.Render("<partial name=\"Missing\" />", NoFiles, new PreviewOptions());
        Assert.Contains("[partial view Missing]", result.Output);
        Assert.Contains(result.Notes, n => n.Contains("'Missing'"));
    }

    [Fact]
    public void AComputedPartialName_IsANote()
    {
        Assert.Contains("[a partial view]", Text("<partial name=\"@Model.Part\" />"));
    }

    [Fact]
    public void APartialThatIncludesItself_StopsAtTheCycle()
    {
        var files = new InMemoryViewFiles(new() { ["Loop"] = "<text>Again</text><partial name=\"Loop\" />" });
        var output = CvwPreview.Render("<partial name=\"Loop\" />", files, new PreviewOptions()).Output;
        Assert.Equal(["Again", "[partial view Loop]"], Lines(output).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void DeeplyNestedPartials_StopAtTheLimit()
    {
        var shared = Enumerable.Range(0, 20).ToDictionary(i => "P" + i, i => $"<partial name=\"P{i + 1}\" />");
        var files = new InMemoryViewFiles(shared);
        CvwPreview.Render("<partial name=\"P0\" />", files, new PreviewOptions());
        Assert.Equal(PreviewBuilder.MaxPartialDepth, files.Requested.Count(name => name.StartsWith('P')));
    }

    [Fact]
    public void TheView_IsDrawnInsideTheDefaultLayout()
    {
        var files = new InMemoryViewFiles(new() { ["_Layout"] = "<text>Top</text><body /><text>Bottom</text>" });
        var output = CvwPreview.Render("<text>Page</text>", files, new PreviewOptions()).Output;
        Assert.Equal(["Top", "Page", "Bottom"], Lines(output).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void TheView_UsesTheLayoutItNames()
    {
        var files = new InMemoryViewFiles(new() { ["_Layout"] = "<text>Wrong</text><body />", ["Plain"] = "<text>Plain</text><body />" });
        var output = CvwPreview.Render("@layout Plain\n<text>Page</text>", files, new PreviewOptions()).Output;
        Assert.Equal(["Plain", "Page"], Lines(output).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void LayoutNone_DrawsTheViewAlone()
    {
        var files = new InMemoryViewFiles(new() { ["_Layout"] = "<text>Top</text><body />" });
        var output = CvwPreview.Render("@layout none\n<text>Page</text>", files, new PreviewOptions()).Output;
        Assert.Equal("Page", output.TrimEnd());
    }

    [Fact]
    public void AMissingNamedLayout_IsANote()
    {
        var result = CvwPreview.Render("@layout Gone\n<text>Page</text>", NoFiles, new PreviewOptions());
        Assert.Equal("Page", result.Output.TrimEnd());
        Assert.Contains(result.Notes, n => n.Contains("'Gone'"));
    }

    [Fact]
    public void ASharedView_IsDrawnWithoutALayout_AndShowsWhereTheBodyGoes()
    {
        var files = new InMemoryViewFiles(new() { ["_Layout"] = "<text>Top</text><body />" }, isShared: true);
        var output = CvwPreview.Render("<text>Top</text><body />", files, new PreviewOptions()).Output;
        Assert.Equal(["Top", "[the page's content]"], Lines(output).Select(l => l.TrimEnd()));
    }

    [Fact]
    public void Html_IsEncoded()
    {
        var html = CvwPreview.Render("<text>&lt;script&gt;alert(1)&lt;/script&gt; &amp; @Model.A</text>", NoFiles,
            new PreviewOptions(Format: PreviewFormat.Html)).Output;
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; {Model.A}", html);
        Assert.StartsWith("<pre class=\"cvw-screen\">", html);
        Assert.EndsWith("</pre>\n", html);
    }

    [Fact]
    public void Html_HasTheColours()
    {
        var html = CvwPreview.Render("<text color=\"#123456\" background=\"cyan\">x</text>", NoFiles,
            new PreviewOptions(Format: PreviewFormat.Html)).Output;
        Assert.Contains("<span style=\"color:#123456;background-color:#00ffff\">x</span>", html);
    }

    [Fact]
    public void AnEmptyView_IsAnEmptyScreen()
    {
        var html = CvwPreview.Render("", NoFiles, new PreviewOptions(Format: PreviewFormat.Html)).Output;
        Assert.Equal("<pre class=\"cvw-screen\"></pre>\n", html);
    }

    [Fact]
    public void Ansi_HasEscapeSequences()
    {
        var ansi = CvwPreview.Render("<text color=\"red\">x</text>", NoFiles, new PreviewOptions(Format: PreviewFormat.Ansi)).Output;
        Assert.Contains("\u001b[", ansi);
    }

    [Fact]
    public void HighContrast_UsesTheHighContrastTheme()
    {
        var normal = CvwPreview.Render("<text class=\"muted\">x</text>", NoFiles, new PreviewOptions(Format: PreviewFormat.Html));
        var high = CvwPreview.Render("<text class=\"muted\">x</text>", NoFiles, new PreviewOptions(Format: PreviewFormat.Html, HighContrast: true));
        Assert.NotEqual(normal.Output, high.Output);
    }

    [Theory]
    [InlineData(1, CvwPreview.MinimumWidth)]
    [InlineData(10_000, CvwPreview.MaximumWidth)]
    public void TheWidth_IsClamped(int asked, int drawn)
    {
        var output = CvwPreview.Render("<separator />", NoFiles, new PreviewOptions(Width: asked)).Output;
        Assert.Equal(drawn, output.ReplaceLineEndings("\n").TrimEnd('\n').Length);
    }

    [Fact]
    public void Diagnostics_HaveTheirLineAndColumn()
    {
        // At the top level an unknown tag is C#, as in the build; inside an element it is an unknown element
        var result = CvwPreview.Render("<text>ok</text>\n<box>\n  <blink>no</blink></box>", NoFiles, new PreviewOptions());
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.True(diagnostic.IsError);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(3, diagnostic.Column);
        Assert.StartsWith("CVW", diagnostic.Id);
        Assert.Contains("ok", result.Output);
    }

    [Fact]
    public void AnUnfinishedView_StillDraws()
    {
        var result = CvwPreview.Render("<text>Typing</text>\n<box title=\"Half", NoFiles, new PreviewOptions());
        Assert.Contains("Typing", result.Output);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void InvalidAttributeValues_AreLeftOut()
    {
        var result = CvwPreview.Render("<box padding=\"lots\"><text>x</text></box>", NoFiles, new PreviewOptions());
        Assert.Contains("│ x │", result.Output);
        Assert.NotEmpty(result.Diagnostics);
    }
}
