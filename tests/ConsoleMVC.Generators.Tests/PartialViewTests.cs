using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests for partial views: <c>Views/Shared/*.cvw</c> and the <c>&lt;partial&gt;</c> element
/// (docs/design/partial-views.md).
/// </summary>
public class PartialViewTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";
    private const string View = "/app/Views/Home/IndexView.cvw";

    private static (string, string) Shared(string name, string text) => ($"/app/Views/Shared/{name}.cvw", text);

    private static void AssertNoErrors(GenerationResult result) =>
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());

    [Fact]
    public void Partial_RendersWithItsModel()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "@model string\n<partial name=\"Greeting\" model=\"@Model\" />\n<text>after</text>" + Quit),
            Shared("Greeting", "@model string\n<text>Hello, @Model</text>")
        ], "\"Ada\"");

        Assert.Equal("Hello, Ada\nafter\n", output);
    }

    [Fact]
    public void Partial_WithoutAModel_AndUsedTwice()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "<partial name=\"Rule\" />\n<text>x</text>\n<partial name=\"Rule\" />" + Quit),
            Shared("Rule", "<text>--</text>")
        ]);

        Assert.Equal("--\nx\n--\n", output);
    }

    [Fact]
    public void Partial_AcceptsALiteralStringModel_AndCSharpInside()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "<partial name=\"Title\" model=\"Settings\" />" + Quit),
            Shared("Title", "@model string\nvar upper = Model.ToUpperInvariant();\n<text>@upper</text>\nif (upper.Length > 0) return;\n<text>never</text>")
        ]);

        Assert.Equal("SETTINGS\n", output);
    }

    [Fact]
    public void Partial_SharesTheViewsForm()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "<partial name=\"NameField\" />\n<text>Got @Form[\"Name\"]</text>" + Quit),
            Shared("NameField", "<input name=\"Name\" label=\"Name: \" />")
        ], input: "Grace");

        Assert.EndsWith("Got Grace\n", output);
    }

    [Fact]
    public void Partials_CanBeNested_AndUseControlFlow()
    {
        var output = ViewHarness.RenderFiles(
        [
            (View, "@model int[]\nforeach (var n in Model) {\n<partial name=\"Item\" model=\"@n\" />\n}" + Quit),
            Shared("Item", "@model int\n<partial name=\"Bullet\" />\n<text>@Model</text>"),
            Shared("Bullet", "<text>*</text>")
        ], "new[] { 1, 2 }");

        Assert.Equal("*\n1\n*\n2\n", output);
    }

    [Fact]
    public void PartialClasses_AreNotRoutableViews_AndNeedNoViewSuffix()
    {
        var result = ViewHarness.GenerateFiles([Shared("Header", "<text>hi</text>")]);

        AssertNoErrors(result);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CVW003");
        var type = result.Assembly!.GetType("TestApp.Views.Shared.Header")!;
        Assert.True(typeof(ConsolePartialView<object>).IsAssignableFrom(type));
        Assert.False(typeof(ConsoleView).IsAssignableFrom(type));
    }

    [Fact]
    public void UnknownPartial_ReportsCVW114()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"Heder\" />" + Quit),
            Shared("Header", "<text>hi</text>")
        ]);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW114");
        Assert.Contains("Header", diagnostic.GetMessage());
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void PartialNameMustBeLiteral_ReportsCVW114()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "var n = \"Header\";\n<partial name=\"@n\" />" + Quit),
            Shared("Header", "<text>hi</text>")
        ]);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW114");
    }

    [Fact]
    public void PartialWithAModel_UsedWithoutOne_ReportsCVW115()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"Greeting\" />" + Quit),
            Shared("Greeting", "@model string\n<text>@Model</text>")
        ]);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW115");
        Assert.Contains("string", diagnostic.GetMessage());
    }

    [Fact]
    public void PartialModelOfTheWrongType_IsACompilerErrorInTheView()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"Count\" model=\"@DateTime.Now\" />" + Quit),
            Shared("Count", "@model int\n<text>@Model</text>")
        ]);

        var error = Assert.Single(result.CompilationDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(View, error.Location.GetMappedLineSpan().Path);
    }

    [Fact]
    public void MenuInAPartial_ReportsCVW103()
    {
        var result = ViewHarness.GenerateFiles(
            [Shared("Nav", "<menu><option key=\"q\" quit>Quit</option></menu>")]);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW103");
        Assert.Contains("partial", diagnostic.GetMessage());
    }

    [Fact]
    public void PartialInsideAnElement_ReportsCVW103()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<box><partial name=\"Header\" /></box>" + Quit),
            Shared("Header", "<text>hi</text>")
        ]);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW103");
    }

    [Fact]
    public void PartialThatIncludesItself_ReportsCVW116()
    {
        var result = ViewHarness.GenerateFiles([Shared("Loop", "<partial name=\"Loop\" />")]);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW116");
    }
}
