using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests for <c>&lt;number&gt;</c>, <c>&lt;date&gt;</c> and <c>&lt;select filter size&gt;</c> in markup.</summary>
public class RicherInputMarkupTests
{
    private const string Post = "\nreturn NavigationResult.ToAction(\"Save\", Form);";

    [Fact]
    public void Number_AndDate_PostTheirValues()
    {
        const string view = """
            <number name="Qty" label="Quantity:" min="1" max="99" step="1" decimals="0" default="1" />
            <number name="Price" label="Price:" min="0.5" decimals="2" />
            <date name="Due" label="Due:" min="2026-01-01" max="2030-12-31" default="2026-06-01" />
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view + Post, input: ["0", "3", "2.25", "2027-03-04"]);

        Assert.StartsWith("Quantity: Enter a number from 1 to 99.\nQuantity: Price: Due: ", output);
        Assert.Equal("3", navigation.FormData!["Qty"]);
        Assert.Equal("2.25", navigation.FormData["Price"]);
        Assert.Equal("2027-03-04", navigation.FormData["Due"]);
    }

    [Fact]
    public void Number_MinAndMax_MayBeExpressions()
    {
        const string view = """
            var limit = 5m;
            <number name="A" max="@limit" />
            """;

        var (_, navigation) = ViewHarness.RenderWithNavigation(view + Post, input: ["6", "5"]);

        Assert.Equal("5", navigation.FormData!["A"]);
    }

    [Fact]
    public void Select_FilterAndSize()
    {
        const string view = """
            <select name="C" label="Country:" filter size="5">
                <choice value="pl">Poland</choice>
                <choice value="pt">Portugal</choice>
            </select>
            """;

        var (_, navigation) = ViewHarness.RenderWithNavigation(view + Post, input: "tug");

        Assert.Equal("pt", navigation.FormData!["C"]);
    }

    [Theory]
    [InlineData("<number name=\"a\" min=\"one\" />")]
    [InlineData("<number name=\"a\" decimals=\"-1\" />")]
    [InlineData("<date name=\"a\" min=\"next week\" />")]
    [InlineData("<date name=\"a\" default=\"2026-02-30\" />")]
    [InlineData("<select name=\"a\" size=\"-2\"><choice>x</choice></select>")]
    public void InvalidValues_AreCVW101(string cvw)
    {
        var result = ViewHarness.Generate(cvw + Post);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW101");
    }

    [Theory]
    [InlineData("<date name=\"a\" min=\"today\" max=\"today+30\" default=\"TODAY+7\" />")]
    [InlineData("<date name=\"a\" min=\"today-365\" />")]
    public void RelativeDates_AreValid(string cvw)
    {
        Assert.Empty(ViewHarness.Generate(cvw + Post).Errors);
    }

    [Theory]
    [InlineData("<box><number name=\"a\" /></box>")]
    [InlineData("<box><date name=\"a\" /></box>")]
    public void Fields_AreTopLevelOnly(string cvw)
    {
        Assert.Contains(ViewHarness.Generate(cvw + Post).GeneratorDiagnostics, d => d.Id == "CVW103");
    }

    [Theory]
    [InlineData("<number label=\"x\" />")]
    [InlineData("<date label=\"x\" />")]
    public void Name_IsRequired(string cvw)
    {
        Assert.Contains(ViewHarness.Generate(cvw + Post).GeneratorDiagnostics, d => d.Id == "CVW111");
    }
}
