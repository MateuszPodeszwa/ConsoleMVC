using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// Tests for <see cref="ValidationErrorsElement"/>.
/// </summary>
public class ValidationErrorsElementTests
{
    private static ModelStateDictionary State()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("Name", "The Name field is required.");
        state.AddModelError("Age", "Age must be between 13 and 120.");
        return state;
    }

    private static string[] Plain(Element element, int width = 40) =>
        element.Layout(width).Select(l => l.ToPlainText()).ToArray();

    [Fact]
    public void ShowsAllErrors_WithBullets()
    {
        Assert.Equal(["• The Name field is required.", "• Age must be between 13 and 120."],
            Plain(new ValidationErrorsElement { Source = State() }));
    }

    [Fact]
    public void For_ShowsOneFieldsErrors_CaseInsensitively()
    {
        Assert.Equal(["• The Name field is required."], Plain(new ValidationErrorsElement { Source = State(), For = "name" }));
    }

    [Fact]
    public void NoErrors_RendersNothing()
    {
        Assert.Empty(Plain(new ValidationErrorsElement { Source = new ModelStateDictionary() }));
        Assert.Empty(Plain(new ValidationErrorsElement()));
        Assert.Equal(0, new ValidationErrorsElement { Source = new ModelStateDictionary() }.MeasureWidth(40));
    }

    [Fact]
    public void LongMessages_WrapWithHangingIndent_AndAreRed()
    {
        var element = new ValidationErrorsElement { Source = State(), For = "Age" };

        Assert.Equal(["• Age must be", "  between 13 and", "  120."], Plain(element, 16));
        Assert.All(element.Layout(16)[0].Spans, s => Assert.Equal(Colour.Red, s.Style.Foreground));
    }

    [Fact]
    public void Messages_AreSanitised()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("x", "bad\u001b[2J");

        Assert.Equal(["• bad"], Plain(new ValidationErrorsElement { Source = state }));
    }
}
