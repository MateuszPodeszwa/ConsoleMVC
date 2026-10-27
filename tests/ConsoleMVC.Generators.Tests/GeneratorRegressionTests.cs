using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Regression tests for generator bugs found in review.
/// </summary>
public class GeneratorRegressionTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private static void AssertNoErrors(GenerationResult result) =>
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());

    // --- generated file names and duplicates ---

    [Fact]
    public void ViewsWithCollidingFileNames_AreAllGenerated()
    {
        var result = ViewHarness.GenerateFiles(
        [
            ("/app/Views/A_B/CView.cvw", "return NavigationResult.Quit();"),
            ("/app/Views/A/B_CView.cvw", "return NavigationResult.Quit();")
        ]);

        AssertNoErrors(result);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
        Assert.NotNull(result.Assembly!.GetType("TestApp.Views.A_B.CView"));
        Assert.NotNull(result.Assembly.GetType("TestApp.Views.A.B_CView"));
    }

    [Fact]
    public void DuplicateViews_ReportCVW004_InsteadOfCrashing()
    {
        var result = ViewHarness.GenerateFiles(
        [
            ("/app/Views/Home/IndexView.cvw", "return NavigationResult.Quit();"),
            ("/app/Areas/X/Views/Home/IndexView.cvw", "return NavigationResult.Quit();")
        ]);

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW004");
        Assert.Contains("namespace TestApp.Views.Home", result.GeneratedSource);
    }

    [Fact]
    public void CaseOnlyDifferentControllerFolders_ReportCVW004()
    {
        var result = ViewHarness.GenerateFiles(
        [
            ("/app/Views/Home/IndexView.cvw", "return NavigationResult.Quit();"),
            ("/app/Views/home/IndexView.cvw", "return NavigationResult.Quit();")
        ]);

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW004");
    }

    // --- #line and #nullable ---

    [Theory]
    [InlineData("/src/Café/Views/Home/IndexView.cvw")]
    [InlineData(@"C:\src\App\Views\Home\IndexView.cvw")]
    public void LineDirectives_UseTheRealPath(string path)
    {
        var result = ViewHarness.Generate("<text>@nope</text>" + Quit, path);

        var error = Assert.Single(result.CompilationDiagnostics, d => d.Id == "CS0103");
        Assert.Equal(path, error.Location.GetMappedLineSpan().Path);
    }

    [Fact]
    public void NullableAnnotations_FollowTheProjectSetting()
    {
        var result = ViewHarness.Generate("@model string?\nstring? s = null;\n<text>@Model @s</text>" + Quit);

        AssertNoErrors(result);
        Assert.DoesNotContain(result.CompilationDiagnostics, d => d.Id == "CS8669");
    }

    // --- parsing ---

    [Theory]
    [InlineData("/* c */")]
    [InlineData("// c\n")]
    public void CommentBeforeElse_IsPartOfTheStatement(string comment)
    {
        var view = "var a = false;\n<box border=\"none\" padding=\"0\">@if (a) { <text>A</text> } " + comment + " else { <text>B</text> }</box>";

        Assert.Equal("B\n", ViewHarness.Render(view + Quit));
    }

    [Fact]
    public void EmailInAttribute_IsLiteral()
    {
        Assert.Equal("┌─ me@example.com ─┐\n└──────────────────┘\n",
            ViewHarness.Render("<box title=\"me@example.com\"></box>" + Quit));
    }

    [Theory]
    [InlineData("#region Customer's data\n<text>x</text>\n#endregion")]
    [InlineData("#warning it's here\n<text>x</text>")]
    [InlineData("var y = 1;\n#if !NEVER_DEFINED\n<text>x</text>\n#endif")]
    public void PreprocessorDirectives_AreSkipped(string body)
    {
        var result = ViewHarness.Generate(body + Quit);

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id.StartsWith("CVW"));
        Assert.Equal("x\n", ViewHarness.Render(body + Quit));
    }

    [Theory]
    [InlineData("/app/Views/class/IndexView.cvw")]
    [InlineData("/app/Views/Home/int.cvw")]
    public void KeywordNames_ReportCVW002(string path)
    {
        var result = ViewHarness.Generate("return NavigationResult.Quit();", path);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW002");
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void InvalidRootNamespace_ReportsCVW005()
    {
        var result = ViewHarness.GenerateFiles([(ViewHarness.DefaultPath, "return NavigationResult.Quit();")], rootNamespace: "App-1.Core");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW005");
        Assert.Null(result.GeneratedSource);
    }

    [Theory]
    [InlineData("<text>a <!-- c --> b</text>")]
    [InlineData("<text>a @* c *@ b</text>")]
    public void WhitespaceCollapses_AcrossComments(string markup)
    {
        Assert.Equal("a b\n", ViewHarness.Render(markup + Quit));
    }

    [Fact]
    public void AdjacentExpressions_AreBothEvaluated()
    {
        Assert.Equal("12\n", ViewHarness.Render("var a = 1; var b = 2;\n<text>@a@b</text>" + Quit));
    }

    [Fact]
    public void UserVariablesNamedLikeOldGeneratedNames_DoNotClash()
    {
        Assert.Equal("5\n", ViewHarness.Render("var __cvw1 = 5;\n<text>@__cvw1</text>" + Quit));
    }

    [Fact]
    public void UnquotedAttributeValues_DecodeEntities()
    {
        Assert.StartsWith("┌─ a&b ─┐", ViewHarness.Render("<box title=a&amp;b></box>" + Quit));
    }

    // --- directives ---

    [Fact]
    public void UsingWithTrailingSemicolon_IsAccepted()
    {
        AssertNoErrors(ViewHarness.Generate("@using System.Text;\nvar b = new StringBuilder();" + Quit));
    }

    [Fact]
    public void DuplicateModel_ReportsCVW006()
    {
        var result = ViewHarness.Generate("@model string\n@model int" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW006");
    }

    [Fact]
    public void ModelDirective_AllowsTabs_AndLeadingComments()
    {
        var result = ViewHarness.Generate("@* Shows the user's name *@\n@model\tstring\n<text>@Model.Length</text>" + Quit);

        AssertNoErrors(result);
    }

    [Fact]
    public void UsingError_PointsAtTheNamespace()
    {
        var result = ViewHarness.Generate("@using   Nope.Ns" + Quit);

        var error = result.CompilationDiagnostics.First(d => d.Id == "CS0246");
        Assert.Equal((0, 9), (error.Location.GetMappedLineSpan().StartLinePosition.Line, error.Location.GetMappedLineSpan().StartLinePosition.Character));
    }

    [Fact]
    public void EmptyExplicitExpression_ReportsCVW112()
    {
        var result = ViewHarness.Generate("<text>@()</text>" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW112");
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void ExtremelyDeepNesting_ReportsCVW113_InsteadOfCrashing()
    {
        var markup = string.Concat(Enumerable.Repeat("<box>", 5000)) + string.Concat(Enumerable.Repeat("</box>", 5000));

        var result = ViewHarness.Generate(markup + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW113");
    }

    // --- found in the 1.9 review ---

    [Fact]
    public void TopLevelMarkupComments_AreIgnored()
    {
        Assert.Equal("a\n1\n", ViewHarness.Render("<!-- heading -->\n<text>a</text>\nvar x = 1;\n<!-- multi\nline -->\n<text>@x</text>" + Quit));
    }

    [Theory]
    [InlineData("<separator>abc</separator>", "separator")]
    [InlineData("<input name=\"a\">x</input>", "input")]
    public void VoidElementsWithContent_ReportCVW103(string cvw, string tag)
    {
        var result = ViewHarness.Generate(cvw + Quit);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW103");
        Assert.Contains($"<{tag} />", diagnostic.GetMessage());
        Assert.DoesNotContain(result.CompilationDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void VoidElementsWithAnEmptyClosingTag_AreAccepted()
    {
        Assert.Equal("\n", ViewHarness.Render("<br>  </br>" + Quit));
    }
}
