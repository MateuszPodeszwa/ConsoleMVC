using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Regression tests for the generator bugs found in the 1.9 review (@inject, partial views and theme classes).
/// </summary>
public class GeneratorReviewRegressionTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";
    private const string View = "/app/Views/Home/IndexView.cvw";
    private const string Service = "namespace TestApp { public interface IFoo { string Name { get; } } }";

    private static void AssertNoErrors(GenerationResult result) =>
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());

    [Theory]
    [InlineData("Theme")]
    [InlineData("PreparePartial")]
    [InlineData("ToString")]
    public void InjectNamesThatHideViewMembers_ReportCVW007(string name)
    {
        var result = ViewHarness.Generate($"@inject TestApp.IFoo {name}\n<text class=\"title\">x</text>" + Quit, extraSource: Service);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW007");
    }

    [Fact]
    public void LiteralPartialModelOfTheWrongType_IsReportedAtTheAttribute()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<text>a</text>\n<partial name=\"P\" model=\"x\" />" + Quit),
            ("/app/Views/Shared/P.cvw", "@model int\n<text>@Model</text>")
        ]);

        var error = Assert.Single(result.CompilationDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var span = error.Location.GetMappedLineSpan();
        Assert.Equal((View, 1, 25), (span.Path, span.StartLinePosition.Line, span.StartLinePosition.Character));
    }

    [Fact]
    public void LowerCaseSharedFolder_HoldsPartials()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"P\" />" + Quit),
            ("/app/Views/shared/P.cvw", "<text>p</text>")
        ]);

        AssertNoErrors(result);
        Assert.NotNull(result.Assembly!.GetType("TestApp.Views.Shared.P"));
    }

    [Fact]
    public void ClashingPartials_UseTheOneThatIsGenerated()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"Header\" />" + Quit),
            ("/app/Views/Shared/header.cvw", "<text>a</text>"),
            ("/app/Z/Views/Shared/Header.cvw", "<text>b</text>")
        ]);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW004");
        Assert.DoesNotContain(result.CompilationDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void PartialWithAnInvalidName_IsNotOffered()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"my-header\" />" + Quit),
            ("/app/Views/Shared/my-header.cvw", "<text>a</text>")
        ]);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW002");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW114");
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void PartialWithErrors_IsReportedOnceInTheView_WithoutCompilerErrors()
    {
        var result = ViewHarness.GenerateFiles(
        [
            (View, "<partial name=\"P\" />" + Quit),
            ("/app/Views/Shared/P.cvw", "<text>a</txt>")
        ]);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW102");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW114" && d.GetMessage().Contains("has errors"));
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("@inject TestApp.IFoo Foo // the service")]
    [InlineData("@inject TestApp.IFoo Foo; // the service")]
    [InlineData("@inject TestApp.IFoo Foo @* svc *@")]
    public void InjectWithATrailingComment_Works(string directive)
    {
        var result = ViewHarness.Generate(directive + "\n<text>@(Foo is null)</text>" + Quit, extraSource: Service);

        AssertNoErrors(result);
    }

    [Fact]
    public void InjectPropertyName_IsMappedToItsColumn()
    {
        var result = ViewHarness.Generate("@inject TestApp.IFoo   IndexView\n<text>x</text>" + Quit, extraSource: Service);

        var error = result.CompilationDiagnostics.First(d => d.Id == "CS0542");
        Assert.Equal(23, error.Location.GetMappedLineSpan().StartLinePosition.Character);
    }

    [Fact]
    public void PartialNamedRender_ReportsCVW009()
    {
        var result = ViewHarness.GenerateFiles([("/app/Views/Shared/Render.cvw", "<text>a</text>")]);

        Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW009");
        Assert.Empty(result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
