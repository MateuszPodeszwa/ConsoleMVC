using ConsoleMVC.Generators.Schema;
using ConsoleMVC.Generators.Tests.Infrastructure;

namespace ConsoleMVC.Generators.Tests;

/// <summary>
/// An attribute written with <c>=</c> and no value (<c>&lt;box title=&gt;</c>) is the same error, CVW101 at the
/// attribute, for every attribute of every element: it is never read as an empty value.
/// </summary>
public class EmptyAttributeValueTests
{
    public static TheoryData<string, string, string> EveryAttribute()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var element in MarkupSchema.Default.Elements)
        {
            foreach (var attribute in element.Attributes)
            {
                data.Add(element.Tag, attribute.Name, ">");
                data.Add(element.Tag, attribute.Name, " />");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryAttribute))]
    public void AttributeWithEqualsAndNoValue_IsCVW101_AtTheAttribute(string tag, string attribute, string end)
    {
        var markup = $"<{tag} {attribute}={end}";
        var result = ViewHarness.Generate(markup + (end == ">" ? $"</{tag}>" : "") + "\nreturn NavigationResult.Quit();");

        var error = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "CVW101");
        Assert.Equal((0, tag.Length + 2), (error.Location.GetLineSpan().StartLinePosition.Line, error.Location.GetLineSpan().StartLinePosition.Character));
        Assert.Contains("'='", error.GetMessage());
        Assert.Null(result.GeneratedSource);
    }

    [Theory]
    [InlineData("<box title=\"\"><text>x</text></box>")]
    [InlineData("<text bold>x</text>")]
    public void AnEmptyQuotedValue_AndAFlag_AreStillAccepted(string markup) =>
        Assert.Empty(ViewHarness.Generate(markup + "\nreturn NavigationResult.Quit();").Errors);
}
