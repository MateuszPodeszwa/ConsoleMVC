using Microsoft.Extensions.DependencyInjection;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>
/// The framework's own words in another language, as docs/guides/docs/localisation.md describes them: YesLetter and
/// NoLetter are "shown in [y/N] and accepted as answers".
/// </summary>
public class LocalisationAcceptanceTests
{
    /// <summary>Polish yes and no: tak and nie.</summary>
    private static MvcTestHost Polish() => MvcTestHost.For<Controllers.HomeController>(builder =>
        builder.Localization.Text = FrameworkText.English with { YesLetter = "t", NoLetter = "n", YesWord = "tak", NoWord = "nie" });

    [Theory]
    [InlineData('t', "agree=True")]
    [InlineData('T', "agree=True")]
    [InlineData('n', "agree=False")]
    public void TheTranslatedLetter_AnswersACheckboxInATerminal(char letter, string answer)
    {
        var run = Polish().Run(new ScriptedInput("5").Key(ScriptedInput.Char(letter)).Line("q"));

        Assert.Contains(answer, run.LastScreen);
    }

    [Theory]
    [InlineData("t", "agree=True")]
    [InlineData("tak", "agree=True")]
    [InlineData("nie", "agree=False")]
    public void TheTranslatedLetterAndWord_AnswerATypedCheckbox(string typed, string answer)
    {
        var run = Polish().Run("5", typed, "q");

        Assert.Contains("[t/N]", run.Screens[1]);
        Assert.Contains(answer, run.LastScreen);
    }

    [Fact]
    public void TheKeysShownBesideAField_AreTranslated()
    {
        var host = MvcTestHost.For<Controllers.HomeController>(builder =>
            builder.Localization.Text = FrameworkText.English with { CheckboxKeys = "(Spacja, Enter)", NumberKeys = "(Góra/Dół, Enter)", FilterKeys = "(pisz, Enter)" });

        // F12 means nothing to the fields; then the input ends while each one waits, so the last screen shows it waiting
        var consent = host.Run(new ScriptedInput("5").Keys(ConsoleKey.F12));
        var quantity = host.Run(new ScriptedInput("4").Keys(ConsoleKey.F12));
        var colour = host.Run(new ScriptedInput("4").Keys(ConsoleKey.Enter, ConsoleKey.F12));

        Assert.Contains("(Spacja, Enter)", consent.LastScreen);
        Assert.Contains("(Góra/Dół, Enter)", quantity.LastScreen);
        Assert.Contains("(pisz, Enter)", colour.LastScreen);
    }
}

/// <summary>
/// Validation messages without an <c>ErrorMessage</c>, translated in resources, as docs/guides/docs/localisation.md
/// describes: as a whole, or by the attribute's English template.
/// </summary>
public class ValidationTranslationAcceptanceTests
{
    private static MvcTestHost Polish() => MvcTestHost.For<Controllers.HomeController>(builder =>
    {
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Localization.Culture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");
        builder.Localization.LocalizeValidation = true;
        builder.Localization.ValidationResource = typeof(AcceptanceText);
    });

    [Fact]
    public void TheAttributesTemplate_IsTranslated_AndFormattedForTheField()
    {
        var run = Polish().Run("1", "", "36", "", "Ada", "36", "", "q");

        Assert.Contains("Pole Name jest wymagane.", run.Screens[2]);
        Assert.DoesNotContain("{0}", run.Screens[2]);
    }

    [Fact]
    public void AWholeMessage_IsTranslatedAsItIs()
    {
        var run = Polish().Run("1", "Ada", "36", "nope", "Ada", "36", "", "q");

        Assert.Contains("Adres e-mail jest nieprawidłowy.", run.Screens[2]);
    }
}
