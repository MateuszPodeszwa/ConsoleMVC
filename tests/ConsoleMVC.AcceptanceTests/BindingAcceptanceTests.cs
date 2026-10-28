namespace Acceptance;

/// <summary>
/// Typed answers bound to action parameters: numbers in the invariant culture with decimal digits only, as
/// docs/guides/docs/controllers-and-forms.md documents ("Invariant culture (1.5)").
/// </summary>
public class BindingAcceptanceTests
{
    private static string Bind(string count, string ratio, string small, string big) =>
        App.Host().Run("2", count, ratio, small, big, "q").LastScreen;

    [Fact]
    public void ValidNumbers_AreBound()
    {
        var screen = Bind("-12", "1.5e3", "0.5", "9000000000");

        Assert.Contains("count=-12 ratio=1500 small=0.5 big=9000000000 errors=0", screen);
    }

    [Theory]
    [InlineData("0x10")]
    [InlineData("#10")]
    [InlineData("&H10")]
    [InlineData("1,000")]
    [InlineData("1.5")]
    public void AWholeNumberWithOtherCharacters_IsAnError(string count)
    {
        var screen = Bind(count, "1", "1", "1");

        Assert.Contains("count=0 ", screen);
        Assert.Contains($"The value '{count}' is not valid for count.", screen);
    }

    [Theory]
    [InlineData("1e400")]
    [InlineData("Infinity")]
    [InlineData("NaN")]
    [InlineData("1,000")]
    public void ANumberThatIsNotFinite_OrHasThousandsSeparators_IsAnError(string value)
    {
        var screen = Bind("1", value, value, "1");

        Assert.Contains($"The value '{value}' is not valid for ratio.", screen);
        Assert.Contains($"The value '{value}' is not valid for small.", screen);
        Assert.Contains("errors=2", screen);
    }

    [Fact]
    public void AWholeNumberField_AsksForAWholeNumber_WhenTheAnswerIsNotANumber()
    {
        // The quantity field has decimals="0": "e.g. 42 or 2.5" would invite an answer it refuses
        var input = new ConsoleMVC.Testing.ScriptedInput("4")
            .Key(ConsoleMVC.Testing.ScriptedInput.Char('-')).Keys(ConsoleKey.Enter)
            .Keys(ConsoleKey.F12);   // means nothing to the field: the input then ends with the error on the screen

        var run = App.Host().Run(input);

        Assert.Contains("Enter a whole number.", run.LastScreen);
        Assert.DoesNotContain("2.5", run.LastScreen);
    }
}
