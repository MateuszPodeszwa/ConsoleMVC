using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Generator tests for navigation markup: <c>&lt;option back&gt;</c> and <c>&lt;breadcrumbs /&gt;</c>.
/// </summary>
public class NavigationMarkupTests
{
    private const string Quit = "\nreturn NavigationResult.Quit();";

    [Fact]
    public void BackOption_ReturnsBack()
    {
        var (_, navigation) = ViewHarness.RenderWithNavigation(
            "<menu><option key=\"b\" back>Back</option><option key=\"q\" quit>Quit</option></menu>", input: "b");

        Assert.True(navigation.IsBack);
    }

    [Theory]
    [InlineData("<option key=\"b\" back nav=\"Home/Index\">x</option>")]
    [InlineData("<option key=\"b\" back quit>x</option>")]
    [InlineData("<option key=\"b\" back=\"false\">x</option>")]
    public void BackOption_MustBeTheOnlyTarget(string option)
    {
        var result = ViewHarness.Generate("<menu>" + option + "</menu>");

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW109" && d.GetMessage().Contains("'back'"));
    }

    [Fact]
    public void Breadcrumbs_RenderNothingOutsideTheApplication()
    {
        // A view created by hand has no navigation history
        Assert.Equal("x\n", ViewHarness.Render("<breadcrumbs />\n<text>x</text>" + Quit));
    }

    [Fact]
    public void Breadcrumbs_AcceptStyling()
    {
        var result = ViewHarness.Generate("<breadcrumbs separator=\" / \" color=\"darkgrey\" class=\"muted\" />" + Quit);

        Assert.True(result.Assembly is not null && !result.Errors.Any(), result.Describe());
        Assert.Contains("Items = this.Breadcrumbs", result.GeneratedSource);
    }
}
