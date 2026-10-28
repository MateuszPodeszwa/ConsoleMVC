using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>Tests that resource lookups (<c>@L["Name"]</c>) work in attributes and text (docs/design/localisation.md).</summary>
public class LocalisedMarkupTests
{
    [Theory]
    [InlineData("<box title=\"@Names[\"Title\"]\"><text>@Names[\"Body\"]</text></box>")]
    [InlineData("<box title=@Names[\"Title\"]><text>@Names[\"Body\"]</text></box>")]
    public void IndexerLookups_WorkInAttributesAndText(string markup)
    {
        const string helper = "var Names = new System.Collections.Generic.Dictionary<string, string> { [\"Title\"] = \"Tytuł\", [\"Body\"] = \"Treść\" };";

        var output = ViewHarness.Render(helper + "\n" + markup + "\nreturn NavigationResult.Quit();");

        Assert.Contains("Tytuł", output);
        Assert.Contains("Treść", output);
    }
}
