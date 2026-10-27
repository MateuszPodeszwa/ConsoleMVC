using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests that mistakes in <c>.cvw</c> files produce clear diagnostics located in the <c>.cvw</c> file.
/// </summary>
public class DiagnosticTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private static Diagnostic Single(string cvw, string id, string path = ViewHarness.DefaultPath)
    {
        var result = ViewHarness.Generate(cvw, path);
        var matches = result.GeneratorDiagnostics.Where(d => d.Id == id).ToList();

        Assert.True(matches.Count == 1, $"Expected one {id}:\n{result.Describe()}");
        return matches[0];
    }

    private static (int Line, int Column) Position(Diagnostic diagnostic)
    {
        var start = diagnostic.Location.GetLineSpan().StartLinePosition;
        return (start.Line, start.Character);
    }

    [Fact]
    public void UnknownAttribute_CVW100_PointsAtAttributeName()
    {
        var diagnostic = Single("@model object\n<box  colur=\"red\"></box>" + Quit, "CVW100");

        Assert.Equal((1, 6), Position(diagnostic));
        Assert.Contains("colur", diagnostic.GetMessage());
        Assert.Contains("colour", diagnostic.GetMessage()); // lists valid attributes
        Assert.Equal(ViewHarness.DefaultPath, diagnostic.Location.GetLineSpan().Path);
    }

    [Theory]
    [InlineData("<text color=\"purple-ish\">x</text>")]
    [InlineData("<box width=\"wide\"></box>")]
    [InlineData("<box border=\"dotted\"></box>")]
    [InlineData("<box padding=\"-1\"></box>")]
    [InlineData("<text bold=\"yes\">x</text>")]
    [InlineData("<box title></box>")]
    [InlineData("<text color=\"a @x\">x</text>")]
    public void InvalidAttributeValue_CVW101(string markup)
    {
        Single(markup + Quit, "CVW101");
    }

    [Theory]
    [InlineData("<box><text>x</box>")]
    [InlineData("<box>")]
    public void UnclosedElement_CVW102(string markup)
    {
        var result = ViewHarness.Generate(markup + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW102");
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void BoxInsideText_CVW103()
    {
        Single("<text><box></box></text>" + Quit, "CVW103");
    }

    [Theory]
    [InlineData("<text>@{ var x = 1; }</text>")]
    [InlineData("<text>@if (true) { <text>x</text> }</text>")]
    public void CodeInsideText_CVW106(string markup)
    {
        Single(markup + Quit, "CVW106");
    }

    [Fact]
    public void UnknownElementInsideBox_CVW105()
    {
        var diagnostic = Single("<box><table></table></box>" + Quit, "CVW105");

        Assert.Contains("&lt;", diagnostic.GetMessage());
    }

    [Fact]
    public void DuplicateAttribute_ViaAlias_CVW107()
    {
        Single("<text color=\"red\" colour=\"blue\">x</text>" + Quit, "CVW107");
    }

    [Fact]
    public void UnterminatedExpression_CVW104()
    {
        Single("<text>@(1 + </text>" + Quit, "CVW104");
    }

    [Theory]
    [InlineData("/app/Pages/IndexView.cvw")]
    [InlineData("/app/Views/IndexView.cvw")]
    [InlineData("/app/Views/Home/Partials/IndexView.cvw")]
    public void MisplacedView_CVW001_IsSkipped(string path)
    {
        var result = ViewHarness.Generate("return NavigationResult.Quit();", path);

        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW001").Severity);
        Assert.Null(result.GeneratedSource);
    }

    [Fact]
    public void ProjectInsideFolderNamedViews_UsesImmediateControllerFolder()
    {
        var result = ViewHarness.Generate("return NavigationResult.Quit();", "/src/Views/MyApp/Views/Home/IndexView.cvw");

        Assert.Contains("namespace TestApp.Views.Home", result.GeneratedSource);
    }

    [Fact]
    public void InvalidControllerFolder_CVW002()
    {
        Single("return NavigationResult.Quit();", "CVW002", "/app/Views/My-Area/IndexView.cvw");
    }

    [Fact]
    public void ViewNameWithoutSuffix_CVW003()
    {
        Single("return NavigationResult.Quit();", "CVW003", "/app/Views/Home/Index.cvw");
    }

    [Fact]
    public void CSharpError_IsReportedAtCvwLine()
    {
        var result = ViewHarness.Generate("@model object\n\nvar x = missingVariable;" + Quit);

        var error = Assert.Single(result.CompilationDiagnostics, d => d.Id == "CS0103");
        var span = error.Location.GetMappedLineSpan();
        Assert.Equal(ViewHarness.DefaultPath, span.Path);
        Assert.Equal(2, span.StartLinePosition.Line);
        Assert.Equal(8, span.StartLinePosition.Character);
    }

    [Fact]
    public void ExpressionError_IsReportedAtCvwLineAndColumn()
    {
        var result = ViewHarness.Generate("@model string\n<text>Hi @Model.Nope</text>" + Quit);

        var error = Assert.Single(result.CompilationDiagnostics, d => d.Id == "CS1061");
        var span = error.Location.GetMappedLineSpan();
        Assert.Equal(1, span.StartLinePosition.Line);
        Assert.Equal(16, span.StartLinePosition.Character);
    }

    [Fact]
    public void UnknownModelType_IsReportedAtModelDirective()
    {
        var result = ViewHarness.Generate("@model Missing.Type\nreturn NavigationResult.Quit();");

        var error = result.CompilationDiagnostics.First(d => d.Id == "CS0246");
        Assert.Equal(0, error.Location.GetMappedLineSpan().StartLinePosition.Line);
        Assert.Equal(ViewHarness.DefaultPath, error.Location.GetMappedLineSpan().Path);
    }
}
