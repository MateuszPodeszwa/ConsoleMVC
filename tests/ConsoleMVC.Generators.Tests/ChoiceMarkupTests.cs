using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// Tests for <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c>, <c>&lt;choice&gt;</c> and <c>&lt;checkbox&gt;</c> in markup
/// (docs/design/choice-inputs.md).
/// </summary>
public class ChoiceMarkupTests
{
    private const string Post = "\nreturn NavigationResult.ToAction(\"Save\", Form);";

    [Fact]
    public void Select_PostsTheChosenValue()
    {
        const string view = """
            <select name="Colour" label="Colour:" default="green">
                <choice value="red">Red</choice>
                <choice value="green">Green</choice>
            </select>
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view + Post, input: "1");

        Assert.Equal("Colour:\n  1) Red\n  2) Green\nChoose 1-2 [Green]: ", output);
        Assert.Equal("red", navigation.FormData!["Colour"]);
    }

    [Fact]
    public void Select_ChoicesFromALoop_AndMultiple()
    {
        const string view = """
            <select name="Ids" label="Pick:" multiple required>
                @foreach (var (id, name) in new[] { (7, "Ada"), (9, "Grace"), (11, "Linus") })
                {
                    <choice value="@id">@name</choice>
                }
            </select>
            """;

        var (_, navigation) = ViewHarness.RenderWithNavigation(view + Post, input: "3,grace");

        Assert.Equal("9,11", navigation.FormData!["Ids"]);
    }

    [Fact]
    public void Radio_AndCheckbox()
    {
        const string view = """
            <radio name="Size" label="Size:"><choice>S</choice><choice>M</choice></radio>
            <checkbox name="Gift" label="Gift wrap" checked />
            """;

        var (output, navigation) = ViewHarness.RenderWithNavigation(view + Post, input: ["m", ""]);

        Assert.Equal("Size: 1) S  2) M\nChoose 1-2: Gift wrap? [Y/n]: ", output);
        Assert.Equal("M", navigation.FormData!["Size"]);
        Assert.Equal("true", navigation.FormData["Gift"]);
    }

    [Fact]
    public void Choices_TakeInlineStyles()
    {
        var result = ViewHarness.Generate("<select name=\"a\"><choice value=\"x\" color=\"red\" bold>X</choice></select>" + Post);

        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("<choice>x</choice>", "only allowed inside <select> or <radio>")]
    [InlineData("<menu><choice>x</choice></menu>", "only allowed inside <select> or <radio>")]
    [InlineData("<box><select name=\"a\"><choice>x</choice></select></box>", "top level")]
    [InlineData("<box><checkbox name=\"a\" /></box>", "top level")]
    [InlineData("<select name=\"a\"><text>x</text></select>", "may only contain <choice>")]
    public void Misplaced_IsCVW103(string cvw, string message)
    {
        var result = ViewHarness.Generate(cvw + Post);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW103" && d.GetMessage().Contains(message));
    }

    [Theory]
    [InlineData("<select><choice>x</choice></select>")]
    [InlineData("<radio><choice>x</choice></radio>")]
    [InlineData("<checkbox label=\"x\" />")]
    public void Name_IsRequired(string cvw)
    {
        var result = ViewHarness.Generate(cvw + Post);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW111");
    }

    [Fact]
    public void Multiple_IsNotAnAttributeOfRadio()
    {
        var result = ViewHarness.Generate("<radio name=\"a\" multiple><choice>x</choice></radio>" + Post);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "CVW100");
    }
}
