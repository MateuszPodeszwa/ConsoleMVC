namespace Acceptance;

/// <summary>Validation as docs/guides/docs/validation.md describes it, through typed answers.</summary>
public class ValidationAcceptanceTests
{
    [Fact]
    public void AnOptionalEmailLeftBlank_IsValid()
    {
        var run = App.Host().Run("1", "Ada", "36", "", "q");

        Assert.Equal(["Home/Index", "Account/Signup", "Account/Save"], run.Routes);
        Assert.Contains("Welcome, Ada. E-mail: [none]", run.LastScreen);
    }

    [Fact]
    public void AnInvalidEmail_IsReported()
    {
        var run = App.Host().Run("1", "Ada", "36", "nope", "Ada", "36", "ada@example.com", "q");

        Assert.Contains("The Email field is not a valid e-mail address.", run.Screens[2]);
        Assert.Contains("Welcome, Ada. E-mail: [ada@example.com]", run.LastScreen);
    }

    [Fact]
    public void ARequiredNameLeftBlank_IsReported()
    {
        var run = App.Host().Run("1", "", "36", "", "Ada", "36", "", "q");

        Assert.Contains("The Name field is required.", run.Screens[2]);
        Assert.Contains("Welcome, Ada.", run.LastScreen);
    }

    [Fact]
    public void AFormShownAgain_KeepsTheValidAnswers_AndAsksAgainForTheWrongOnes()
    {
        // Name is missing; Age and the e-mail address are fine
        var input = new ConsoleMVC.Testing.ScriptedInput("1", "", "41", "ada@example.com")
            .Line("Ada")    // the name, asked again
            .Line("")       // Enter keeps the age posted before
            .Line("")       // and the e-mail address
            .Line("q");

        var run = App.Host().Run(input);

        Assert.Contains("Welcome, Ada. E-mail: [ada@example.com]", run.LastScreen);
    }

    [Fact]
    public void AWrongAnswer_IsNotFilledInAgain()
    {
        // The e-mail address is wrong: shown again, Enter leaves it empty (it is optional), rather than keeping "nope"
        var run = App.Host().Run("1", "Ada", "41", "nope", "", "", "", "q");

        Assert.Contains("Welcome, Ada. E-mail: [none]", run.LastScreen);
    }
}
