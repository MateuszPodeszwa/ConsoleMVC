using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for <c>&lt;scroll&gt;</c> (docs/design/full-screen.md).</summary>
public class ScrollMarkupTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    [Fact]
    public void Scroll_DrawsItsContent_WhenKeysCannotBeRead()
    {
        const string view = """
            <scroll height="2" colour="grey" class="muted">
                @for (var i = 1; i <= 3; i++)
                {
                    <text>Line @i</text>
                }
            </scroll>
            <text>after</text>
            """;

        Assert.Equal("Line 1\nLine 2\nLine 3\nafter\n", ViewHarness.Render(view + Quit));
    }

    [Theory]
    [InlineData("<box><scroll><text>x</text></scroll></box>")]
    [InlineData("<scroll height=\"0\"><text>x</text></scroll>")]
    public void Scroll_RejectsInvalidMarkup(string markup)
    {
        Assert.NotEmpty(ViewHarness.Generate(markup + Quit).GeneratorDiagnostics);
    }

    [Fact]
    public void Scroll_WorksInPartials()
    {
        var result = ViewHarness.GenerateFiles(
        [
            ("/app/Views/Home/IndexView.cvw", "<text>x</text>" + Quit),
            ("/app/Views/Shared/Log.cvw", "<scroll><text>x</text></scroll>")
        ]);

        Assert.True(result.Assembly is not null, result.Describe()); // a partial shares its view's input, so it may page too
    }
}
