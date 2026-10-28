using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Mistakes in directives and in the view's C# that the compiler would report in the generated file (raw CSxxxx errors,
/// often at lines the view does not have) are CVW diagnostics in the <c>.cvw</c> file instead.
/// </summary>
public class DirectiveDiagnosticTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private static Diagnostic Single(string cvw, string id, string path = ViewHarness.DefaultPath)
    {
        var result = ViewHarness.Generate(cvw, path);
        var matches = result.GeneratorDiagnostics.Where(d => d.Id == id).ToList();

        Assert.True(matches.Count == 1, $"Expected one {id}:\n{result.Describe()}");
        Assert.DoesNotContain(result.CompilationDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        return matches[0];
    }

    private static (int Line, int Column) Position(Diagnostic diagnostic)
    {
        var start = diagnostic.Location.GetLineSpan().StartLinePosition;
        return (start.Line, start.Character);
    }

    // ------------------------------------------------------------------ directives without a value

    [Theory]
    [InlineData("@refresh", "CVW010")]
    [InlineData("@layout", "CVW117")]
    [InlineData("@model", "CVW011")]
    [InlineData("@using", "CVW011")]
    [InlineData("  @refresh  ", "CVW010")]
    public void DirectiveWithoutAValue_IsACvwError_AtTheDirective(string directive, string id)
    {
        var diagnostic = Single(directive + "\n<text>x</text>" + Quit, id);

        Assert.Equal((0, directive.IndexOf('@')), Position(diagnostic));
        Assert.Contains("e.g.", diagnostic.GetMessage());
    }

    // ------------------------------------------------------------------ @inject type

    [Theory]
    [InlineData("@inject System.String Foo Bar")]
    [InlineData("@inject System.IServiceProvider) Provider")]
    [InlineData("@inject List<int Numbers")]
    public void InjectWhoseTypeIsNotAType_IsCVW007_AtTheType(string directive)
    {
        var diagnostic = Single(directive + "\n<text>x</text>" + Quit, "CVW007");

        Assert.Equal((0, "@inject ".Length), Position(diagnostic));
        Assert.Contains("type", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("@inject System.Collections.Generic.Dictionary<string, int> Limits")]
    [InlineData("@inject System.IServiceProvider? Provider")]
    [InlineData("@inject global::System.IServiceProvider Provider")]
    [InlineData("@inject System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, int[]>> Tables")]
    public void InjectWithAnyRealType_StillCompiles(string directive) =>
        Assert.Empty(ViewHarness.Generate(directive + "\n<text>x</text>" + Quit).Errors);

    // ------------------------------------------------------------------ a view with no result

    [Theory]
    [InlineData("<text>x</text>")]
    [InlineData("<text>x</text>\nvar total = 1 + 2;")]
    [InlineData("@model string\n<box><text>@Model</text></box>\n")]
    [InlineData("")]
    public void ViewWithNoMenuAndNoReturn_IsCVW012(string cvw)
    {
        var diagnostic = Single(cvw, "CVW012");

        Assert.Contains("<menu>", diagnostic.GetMessage());
        Assert.Contains("return NavigationResult", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("<text>x</text>" + Quit)]
    [InlineData("<menu><option key=\"q\" quit>Quit</option></menu>")]
    [InlineData("<text>x</text>\nthrow new System.InvalidOperationException();")]
    [InlineData("<text>x</text>\nwhile (true) { System.Console.ReadLine(); }")]
    public void ViewThatEndsWithAResult_Compiles(string cvw) =>
        Assert.Empty(ViewHarness.Generate(cvw).Errors);

    [Fact]
    public void PartialViewWithNoReturn_Compiles() =>
        Assert.Empty(ViewHarness.Generate("<text>x</text>", "/app/Views/Shared/Header.cvw").Errors);

    [Fact]
    public void ViewWithAReturnOnOnlySomePaths_IsStillReportedByTheCompiler_AtTheView()
    {
        // The view has a return, so the generator leaves the paths to the compiler
        var result = ViewHarness.Generate("<text>x</text>\nif (Model is null) return NavigationResult.Quit();");

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CVW012");
        Assert.Contains(result.CompilationDiagnostics, d => d.Id == "CS0161" && d.Location.GetMappedLineSpan().Path == ViewHarness.DefaultPath);
    }

    // ------------------------------------------------------------------ @ before C# at the top level

    [Theory]
    [InlineData("@if (true) { <text>x</text> }", "@if")]
    [InlineData("@foreach (var i in new[] { 1 }) { <text>@i</text> }", "@foreach")]
    [InlineData("@{ var x = 1; }", "@{")]
    [InlineData("var n = 1;\n@(n + 1);", "@(")]
    [InlineData("<box>@{ @if (true) { <text>x</text> } }</box>", "@if")]
    public void RazorStyleAt_InCSharp_IsCVW122_SayingToWriteCSharpWithoutIt(string body, string written)
    {
        var cvw = body + Quit;
        var diagnostic = Single(cvw, "CVW122");

        var at = cvw.IndexOf(written, StringComparison.Ordinal);
        if (body.StartsWith("<box>", StringComparison.Ordinal))
            at = cvw.LastIndexOf(written, StringComparison.Ordinal);
        Assert.Equal((cvw[..at].Count(c => c == '\n'), at - (cvw.LastIndexOf('\n', at) + 1)), Position(diagnostic));
        Assert.Contains("without '@'", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("var @class = 1;\n<text>@@class @(@class)</text>")]
    [InlineData("var path = @\"C:\\temp\";\n<text>@path</text>")]
    [InlineData("var text = $@\"{1}\";\n<text>@text</text>")]
    [InlineData("if (true) { <text>x</text> }")]
    public void VerbatimIdentifiersAndStrings_AreStillCSharp(string body) =>
        Assert.Empty(ViewHarness.Generate(body + Quit).Errors);
}
