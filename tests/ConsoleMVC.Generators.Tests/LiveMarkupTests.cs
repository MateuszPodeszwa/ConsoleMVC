using ConsoleMVC.Generators.Tests.Infrastructure;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for <c>&lt;progress&gt;</c>, <c>&lt;spinner&gt;</c> and <c>@refresh</c> (docs/design/live-elements.md).</summary>
public class LiveMarkupTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    private static TimeSpan? RefreshOf(GenerationResult result)
    {
        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        var type = result.Assembly!.GetTypes().Single(t => t.Name == "IndexView");
        return ((ConsoleView)Activator.CreateInstance(type)!).DeclaredRefreshInterval;
    }

    // ------------------------------------------------------------------ <progress> and <spinner>

    [Fact]
    public void Progress_TakesLiteralsAndExpressions()
    {
        var output = ViewHarness.Render(
            "@model int\n<progress label=\"Copy \" value=\"@Model\" max=\"8\" bar-width=\"8\" />\n" +
            "<progress value=\"2.5\" max=\"10\" bar-width=\"4\" percent=\"false\" style=\"ascii\" color=\"green\" />" + Quit, "6");

        Assert.Equal("Copy ██████░░  75%\n#---\n", output);
    }

    [Fact]
    public void Spinner_ShowsItsFrame_OrDone()
    {
        var output = ViewHarness.Render(
            "<spinner label=\"Loading\" style=\"line\" frame=\"2\" colour=\"cyan\" label-colour=\"grey\" />\n" +
            "<spinner label=\"Ready\" done />\n<spinner label=\"Ok\" done done-symbol=\"[ok]\" />" + Quit);

        Assert.Equal("- Loading\n✓ Ready\n[ok] Ok\n", output);
    }

    [Theory]
    [InlineData("<progress value=\"lots\" />")]
    [InlineData("<progress bar-width=\"0\" />")]
    [InlineData("<progress>50</progress>")]
    [InlineData("<spinner style=\"wobbly\" />")]
    public void LiveElements_RejectInvalidAttributes(string markup)
    {
        var result = ViewHarness.Generate(markup + Quit);

        Assert.NotEmpty(result.GeneratorDiagnostics);
    }

    // ------------------------------------------------------------------ @refresh

    [Theory]
    [InlineData("500ms", 500)]
    [InlineData("5s", 5000)]
    [InlineData("1.5s", 1500)]
    [InlineData("2m", 120_000)]
    [InlineData("100ms", 100)]
    public void Refresh_SetsTheViewsInterval(string value, int milliseconds)
    {
        var result = ViewHarness.Generate($"@refresh {value}\n<text>x</text>" + Quit);

        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), RefreshOf(result));
    }

    [Fact]
    public void Views_DoNotRefresh_WithoutTheDirective()
    {
        Assert.Null(RefreshOf(ViewHarness.Generate("<text>x</text>" + Quit)));
    }

    [Theory]
    [InlineData("5")]
    [InlineData("soon")]
    [InlineData("50ms")]
    [InlineData("25h")]
    [InlineData("-1s")]
    [InlineData("1,5s")]
    public void Refresh_RejectsIntervalsItCannotUse(string value)
    {
        var result = ViewHarness.Generate($"@refresh {value}\n<text>x</text>" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW010");
    }

    [Fact]
    public void Refresh_BelongsToViews_NotPartialsOrLayouts()
    {
        var result = ViewHarness.GenerateFiles(
        [
            ("/app/Views/Home/IndexView.cvw", "<text>x</text>" + Quit),
            ("/app/Views/Shared/Header.cvw", "@refresh 5s\n<text>header</text>"),
            ("/app/Views/Shared/_Layout.cvw", "@refresh 5s\n<body />")
        ]);

        Assert.Equal(2, result.GeneratorDiagnostics.Count(d => d.Id == "CVW010"));
    }

    [Fact]
    public void Refresh_MayAppearOnlyOnce()
    {
        var result = ViewHarness.Generate("@refresh 5s\n@refresh 2s\n<text>x</text>" + Quit);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW010");
    }
}
