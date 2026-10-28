using ConsoleMVC.Generators.Tests.Infrastructure;
using Microsoft.CodeAnalysis;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Number attributes (<c>&lt;number min max step&gt;</c>, <c>&lt;progress value max&gt;</c>, <c>&lt;bar value&gt;</c>,
/// <c>&lt;bar-chart max&gt;</c>, <c>&lt;sparkline min max&gt;</c>) take an expression of any number type, whatever the
/// element's property is (<c>decimal</c> or <c>double</c>).
/// </summary>
public class NumberExpressionTests
{
    private const string Post = "\nreturn NavigationResult.ToAction(\"Save\", Form);";

    [Fact]
    public void Number_RangeAndStep_TakeDoubleExpressions()
    {
        var (output, navigation) = ViewHarness.RenderWithNavigation(
            "<number name=\"n\" min=\"@(0.5)\" max=\"@(3.5)\" step=\"@(0.5)\" />" + Post, input: ["4", "3.5"]);

        Assert.Contains("0.5", output);
        Assert.Contains("3.5", output); // the range in the error for 4
        Assert.Equal("3.5", navigation.FormData!["n"]);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("short")]
    [InlineData("byte")]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("decimal")]
    [InlineData("int?")]
    [InlineData("double?")]
    [InlineData("decimal?")]
    public void Number_Max_TakesAnyNumberType(string type)
    {
        var (_, navigation) = ViewHarness.RenderWithNavigation(
            $"{type} limit = 2;\n<number name=\"n\" max=\"@limit\" step=\"@((byte)1)\" />" + Post, input: ["3", "2"]);

        Assert.Equal("2", navigation.FormData!["n"]);
    }

    [Fact]
    public void Number_NullableMax_ThatIsNull_MeansNoMaximum()
    {
        var (_, navigation) = ViewHarness.RenderWithNavigation(
            "double? limit = null;\n<number name=\"n\" max=\"@limit\" />" + Post, input: ["1000000"]);

        Assert.Equal("1000000", navigation.FormData!["n"]);
    }

    [Theory]
    [InlineData("<progress value=\"@(3.5m)\" max=\"@(7L)\" bar-width=\"10\" percent />", "50%")]
    [InlineData("<bar-chart max=\"@(10m)\" values><bar label=\"A\" value=\"@(5m)\" /></bar-chart>", "A")]
    [InlineData("<sparkline values=\"1 2 3\" min=\"@(1m)\" max=\"@(3)\" />", "▁")]
    public void DoubleAttributes_TakeDecimalAndWholeNumberExpressions(string markup, string expected) =>
        Assert.Contains(expected, ViewHarness.Render(markup + "\nreturn NavigationResult.Quit();"));

    [Fact]
    public void NonNumberExpression_IsACompileError_AtTheAttribute()
    {
        const string markup = "<number name=\"n\" min=\"@(\"one\")\" />";
        var result = ViewHarness.Generate(markup + "\nreturn NavigationResult.Quit();");
        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.NotEmpty(errors);
        Assert.All(errors, error =>
        {
            var position = error.Location.GetMappedLineSpan();
            Assert.Equal(ViewHarness.DefaultPath, position.Path);
            Assert.Equal(0, position.StartLinePosition.Line);
            Assert.Equal(markup.IndexOf("min=", StringComparison.Ordinal), position.StartLinePosition.Character);
        });
    }
}
