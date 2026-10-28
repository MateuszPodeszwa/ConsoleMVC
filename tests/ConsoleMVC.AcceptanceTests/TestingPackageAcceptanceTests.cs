using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>The ConsoleMVC.Testing package itself, as docs/guides/docs/testing.md describes it.</summary>
public class TestingPackageAcceptanceTests
{
    private static MvcTestHost MouseHost() => MvcTestHost.For<Controllers.HomeController>(builder => builder.Mouse = true);

    [Fact]
    public void AClick_AnswersAMenu_WithTheMouseOn()
    {
        var run = MouseHost().Run(new ScriptedInput().Click(1, 3).Line("q"));   // "[1] Sign up" is the fourth row

        Assert.Equal("Account/Signup", run.Routes[1]);
    }

    [Fact]
    public void AClick_WithTheMouseOff_FailsWithAMessageSayingSo()
    {
        var error = Assert.Throws<InvalidOperationException>(() => App.Host().Run(new ScriptedInput().Click(1, 3)));

        Assert.Contains("builder.Mouse = true", error.Message);
    }

    [Fact]
    public void AKeyWhereALineIsRead_FailsWithAMessageNamingBoth()
    {
        var error = Assert.Throws<InvalidOperationException>(() => App.Host().Run(new ScriptedInput().Key(ScriptedInput.Char('A'))));

        Assert.Contains("read a line", error.Message);
        Assert.Contains("'A'", error.Message);
    }

    [Fact]
    public void RemainingInput_CountsTheStepsNeverRead()
    {
        var run = App.Host().Run("q", "extra", "more");

        Assert.Equal(2, run.RemainingInput);
    }
}
