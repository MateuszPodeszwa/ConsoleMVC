namespace Acceptance;

/// <summary>
/// What happens when the input ends (a pipe runs out, or Ctrl+D) while the application waits: the run ends, whether
/// a menu or a form field was waiting, and a form is never posted with answers nobody gave.
/// </summary>
public class EndOfInputAcceptanceTests
{
    [Fact]
    public void InputEndingInAForm_EndsTheRunWithoutPosting()
    {
        var run = App.Host().Run("1", "Ada");   // the sign-up form, then nothing more after the name

        Assert.Equal(["Home/Index", "Account/Signup"], run.Routes);
    }

    [Fact]
    public void InputEndingInAFormShownAgain_EndsTheRun()
    {
        // The form fails validation and is shown again; then the input ends. It must not repeat for ever.
        var run = App.Host().Run("1", "", "36", "");

        Assert.Equal(["Home/Index", "Account/Signup", "Account/Save"], run.Routes);
        Assert.Contains("The Name field is required.", run.LastScreen);
    }

    [Fact]
    public void InputEndingAtAMenu_EndsTheRun()
    {
        var run = App.Host().Run();

        Assert.Equal(["Home/Index"], run.Routes);
    }
}
