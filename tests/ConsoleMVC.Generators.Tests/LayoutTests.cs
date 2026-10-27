using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests for layout views: a <c>Views/Shared</c> file with a <c>&lt;body /&gt;</c> placeholder that frames every view
/// (docs/design/layout-views.md).
/// </summary>
public class LayoutTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";
    private const string View = "/app/Views/Home/IndexView.cvw";

    private static (string, string) Shared(string name, string text) => ($"/app/Views/Shared/{name}.cvw", text);

    private const string MainLayout = "<text>== header ==</text>\n<body />\n<text>== footer ==</text>";

    private static void AssertNoErrors(GenerationResult result) =>
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());

    [Fact]
    public void DefaultLayout_FramesAViewWithoutInteraction()
    {
        var output = ViewHarness.RenderFiles([(View, "<text>content</text>" + Quit), Shared("_Layout", MainLayout)]);

        Assert.Equal("== header ==\ncontent\n== footer ==\n", output);
    }

    [Fact]
    public void Footer_IsDrawnBeforeTheFirstPrompt()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "<text>content</text>\n<input name=\"Name\" label=\"Name: \" />\n<text>after @Form[\"Name\"]</text>" + Quit),
            Shared("_Layout", MainLayout)
        ], input: "Ada");

        Assert.Equal("== header ==\ncontent\n== footer ==\nName: after Ada\n", output);
    }

    [Fact]
    public void Footer_IsDrawnBeforeAMenu()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "<text>content</text>\n<menu><option key=\"q\" quit>Quit</option></menu>"),
            Shared("_Layout", MainLayout)
        ], input: "q");

        Assert.StartsWith("== header ==\ncontent\n== footer ==\n[q] Quit\n", output);
    }

    [Fact]
    public void NamedLayout_AndLayoutNone()
    {
        var files = new[]
        {
            (View, "@layout Plain\n<text>content</text>" + Quit),
            ("/app/Views/Home/AboutView.cvw", "@layout none\n<text>about</text>" + Quit),
            Shared("_Layout", MainLayout),
            Shared("Plain", "<text>plain</text>\n<body />")
        };

        Assert.Equal("plain\ncontent\n", ViewHarness.RenderFiles(files));
        var result = ViewHarness.GenerateFiles(files);
        AssertNoErrors(result);
        Assert.DoesNotContain("UseLayout", Section(result.GeneratedSource!, "class AboutView"));
    }

    [Fact]
    public void Layouts_ShareTheViewsState_AndMayUseCode()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "ViewData[\"Title\"] = \"Orders\";\n<text>content</text>" + Quit),
            Shared("_Layout", "var title = ViewData.GetValueOrDefault(\"Title\") ?? \"?\";\n<text>[@title]</text>\n<body />")
        ]);

        // The header runs before the view's own code, so it sees ViewData from the controller, not from the view
        Assert.Equal("[?]\ncontent\n", output);
    }

    [Fact]
    public void LayoutClass_IsALayoutView()
    {
        var result = ViewHarness.GenerateFiles([Shared("_Layout", MainLayout)]);

        AssertNoErrors(result);
        Assert.True(typeof(ConsoleLayoutView).IsAssignableFrom(result.Assembly!.GetType("TestApp.Views.Shared._Layout")));
    }

    [Fact]
    public void PartialsDoNotGetTheDefaultLayout()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "<partial name=\"Card\" />" + Quit),
            Shared("_Layout", MainLayout),
            Shared("Card", "<text>card</text>")
        ]);

        Assert.Equal("== header ==\ncard\n== footer ==\n", output);
    }

    [Theory]
    [InlineData("<body />", "Views/Home/IndexView.cvw")]                         // not in Views/Shared
    [InlineData("<text>a</text>\n<body />\n<body />", "Views/Shared/L.cvw")]    // twice
    [InlineData("if (true) {\n<body />\n}", "Views/Shared/L.cvw")]              // inside a C# block
    [InlineData("<box><body /></box>", "Views/Shared/L.cvw")]                   // inside an element
    public void MisplacedBody_IsAnError(string cvw, string path)
    {
        var text = path.Contains("Home") ? cvw + Quit : cvw;
        var result = ViewHarness.GenerateFiles([("/app/" + path, text)]);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id is "CVW117" or "CVW103");
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void UnknownLayout_ReportsCVW118()
    {
        var result = ViewHarness.GenerateFiles([(View, "@layout Missing\n<text>x</text>" + Quit), Shared("Plain", "<body />")]);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW118");
        Assert.Contains("Plain", diagnostic.GetMessage());
    }

    [Fact]
    public void LayoutOnAPartialThatIsNotALayout_ReportsCVW118()
    {
        var result = ViewHarness.GenerateFiles([(View, "@layout Card\n<text>x</text>" + Quit), Shared("Card", "<text>c</text>")]);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW118");
    }

    [Fact]
    public void PartialElementNamingALayout_ReportsCVW114()
    {
        var result = ViewHarness.GenerateFiles([(View, "<partial name=\"_Layout\" />" + Quit), Shared("_Layout", MainLayout)]);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW114" && d.GetMessage().Contains("layout"));
    }

    [Fact]
    public void LayoutWithAModel_ReportsCVW117()
    {
        var result = ViewHarness.GenerateFiles([Shared("_Layout", "@model string\n<body />")]);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW117");
    }

    private static string Section(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        var end = source.IndexOf("// <auto-generated>", start, StringComparison.Ordinal);
        return end < 0 ? source[start..] : source[start..end];
    }
}
