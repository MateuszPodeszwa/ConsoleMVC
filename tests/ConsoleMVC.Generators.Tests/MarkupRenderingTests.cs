using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// End-to-end tests: <c>.cvw</c> source → generator → compiler → executed view → plain-text output.
/// </summary>
public class MarkupRenderingTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private const string Models = """
        using System.Collections.Generic;

        namespace TestApp
        {
            public class Dashboard
            {
                public string User { get; set; } = "Ada";
                public List<string> Items { get; set; } = ["one", "two"];
                public int Count { get; set; } = 3;
                public bool Admin { get; set; }
            }
        }
        """;

    [Fact]
    public void Text_RendersLiteralsAndExpressions()
    {
        var output = ViewHarness.Render("@model string\n<text>Hello, @Model!</text>" + Quit, "\"Ada\"");

        Assert.Equal("Hello, Ada!\n", output);
    }

    [Fact]
    public void Text_CollapsesSourceWhitespace()
    {
        const string view = """
            @model string
            <text>
                Hello,
                    @Model
                — welcome!
            </text>
            """;

        Assert.Equal("Hello, Ada — welcome!\n", ViewHarness.Render(view + Quit, "\"Ada\""));
    }

    [Fact]
    public void Text_LineBreaks()
    {
        Assert.Equal("one\ntwo\n\nthree\n", ViewHarness.Render("<text>one<br/>two <br count=\"2\" /> three</text>" + Quit));
    }

    [Fact]
    public void Box_WithTitleSeparatorAndLoop()
    {
        const string view = """
            @model TestApp.Dashboard

            <box title="Hi @Model.User" padding="1">
                <text>Items: @Model.Count</text>
                <separator />
                @foreach (var item in Model.Items)
                {
                    <text>- @item</text>
                }
            </box>
            """;

        var output = ViewHarness.Render(view + Quit, "new TestApp.Dashboard()", Models);

        Assert.Equal(
            """
            ┌─ Hi Ada ─┐
            │ Items: 3 │
            │ ──────── │
            │ - one    │
            │ - two    │
            └──────────┘

            """.Replace("\r\n", "\n"), output);
    }

    [Fact]
    public void IfElse_InsideBox()
    {
        const string view = """
            @model TestApp.Dashboard
            <box border="ascii" padding="0">
                @if (Model.Admin) {
                    <text>admin</text>
                } else {
                    <text>user</text>
                }
            </box>
            """;

        Assert.Equal("+----+\n|user|\n+----+\n", ViewHarness.Render(view + Quit, "new TestApp.Dashboard()", Models));
    }

    [Fact]
    public void CodeBlock_InsideBox_DeclaresVariables()
    {
        const string view = """
            <box border="none" padding="0">
                @{ var greeting = "hey"; }
                <text>@greeting</text>
            </box>
            """;

        Assert.Equal("hey\n", ViewHarness.Render(view + Quit));
    }

    [Fact]
    public void ImplicitText_InsideBox()
    {
        Assert.Equal("┌─────┐\n│ a 3 │\n└─────┘\n", ViewHarness.Render("<box> a @(1 + 2) </box>" + Quit));
    }

    [Fact]
    public void AttributeExpressions_AreAssigned()
    {
        const string view = """
            @model TestApp.Dashboard
            <box width=@(Model.Count * 4) border="ascii" padding=@(0)>
                <text align=@ConsoleMVC.Rendering.Alignment.Right>@Model.Count</text>
            </box>
            """;

        Assert.Equal("+----------+\n|         3|\n+----------+\n", ViewHarness.Render(view + Quit, "new TestApp.Dashboard()", Models));
    }

    [Fact]
    public void Markup_InterleavesWithCode_InSourceOrder()
    {
        const string view = """
            var name = "first";
            <text>@name</text>
            name = "second";
            <text>@name</text>
            switch (name)
            {
                case "second":
                    <text>switched</text>
                    break;
            }
            """;

        Assert.Equal("first\nsecond\nswitched\n", ViewHarness.Render(view + Quit));
    }

    [Fact]
    public void RazorComments_AreRemovedFromCode()
    {
        const string view = """
            @* A comment mentioning <box> and "quotes" *@
            var shown = "yes"; @* trailing comment *@
            <text>@shown</text>
            """;

        Assert.Equal("yes\n", ViewHarness.Render(view + Quit));
    }

    [Fact]
    public void ModelData_IsSanitised()
    {
        Assert.Equal("Bob[2J\n", ViewHarness.Render("@model string\n<text>@Model</text>" + Quit, "\"Bob\\u001b[2J\""));
    }

    [Fact]
    public void PlainCSharpView_RemainsBackwardsCompatible()
    {
        const string view = """
            @model string
            @using System.Text

            var builder = new StringBuilder(Model);
            return builder.Length > 2
                ? NavigationResult.ToAction("Long")
                : NavigationResult.Quit();
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view, "\"Ada\"");

        Assert.Equal("", output);
        Assert.Equal("Long", navigation.Action);
    }

    [Fact]
    public void FormDataNavigation_StillCompiles()
    {
        const string view = """
            var data = new Dictionary<string, string> { ["Name"] = "Ada" };
            return NavigationResult.ToAction("Result", data);
            """;

        var (_, navigation) = ViewHarness.RenderWithNavigation(view);

        Assert.Equal("Ada", navigation.FormData!["Name"]);
    }

    [Fact]
    public void GeneratedCode_HasNoWarnings_WithOrWithoutImplicitUsings()
    {
        const string globalUsings = "global using System; global using System.Linq; global using System.Collections.Generic;";
        const string view = "@using System.Linq\n<text bold color=\"cyan\">x</text>" + Quit;

        foreach (var extra in new[] { "", globalUsings })
        {
            var result = ViewHarness.Generate(view, extraSource: extra);

            Assert.True(result.Assembly is not null, result.Describe());
            Assert.DoesNotContain(result.CompilationDiagnostics, d => d.Severity >= Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
        }
    }
}
