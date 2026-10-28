using ConsoleMVC.Mvc;
using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>Keyboard shortcuts, as docs/guides/docs/controllers-and-forms.md describes them.</summary>
public class ShortcutAcceptanceTests
{
    [Theory]
    [InlineData("Ctrl+C")]
    [InlineData("Ctrl+D")]
    [InlineData("Ctrl+Z")]
    [InlineData("Ctrl+H")]
    [InlineData("Ctrl+I")]
    [InlineData("Ctrl+J")]
    [InlineData("Ctrl+M")]
    [InlineData("Ctrl+Q")]
    [InlineData("Ctrl+S")]
    [InlineData("Ctrl+Shift+S")]
    public void KeysATerminalUsesItself_AreRefused(string gesture)
    {
        var host = App.Host();
        host.ConfigureApplication = app => app.MapKey(KeyGesture.Parse(gesture), NavigationResult.Quit(), "Quit");

        Assert.Throws<ArgumentException>(() => host.Run("q"));
    }

    [Fact]
    public void AShortcut_WorksWhileAMenuWaits()
    {
        var host = App.Host();
        host.ConfigureApplication = app => app.MapKey(KeyGesture.Parse("Ctrl+O"), NavigationResult.To("Orders/Index"), "Orders");

        var run = host.Run(new ScriptedInput().Key(new ConsoleKeyInfo('\x0f', ConsoleKey.O, false, false, true)).Line("q"));

        Assert.Equal(["Home/Index", "Orders/Index"], run.Routes);
    }

    /// <summary>The host with Esc mapped to Back, as the template maps it.</summary>
    private static MvcTestHost WithEscForBack()
    {
        var host = App.Host();
        host.ConfigureApplication = app => app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back");
        return host;
    }

    private static ConsoleKeyInfo Esc => new('\u001b', ConsoleKey.Escape, false, false, false);

    [Fact]
    public void EscInAChangedNumber_RestoresTheDefault_RatherThanGoingBack()
    {
        var input = new ScriptedInput("4")
            .Key(ScriptedInput.Char('4')).Key(ScriptedInput.Char('5')).Key(Esc).Keys(ConsoleKey.Enter)   // 45, Esc: back to 1
            .Keys(ConsoleKey.Enter)                                                                       // the first colour
            .Line("q");

        var run = WithEscForBack().Run(input);

        Assert.Equal(["Home/Index", "Quantity/Index", "Quantity/Show"], run.Routes);
        Assert.Contains("quantity=1 colour=Red", run.LastScreen);
    }

    [Fact]
    public void EscInANumberAtItsDefault_IsTheShortcut()
    {
        var run = WithEscForBack().Run(new ScriptedInput("4").Key(Esc).Line("q"));

        Assert.Equal(["Home/Index", "Quantity/Index", "Home/Index"], run.Routes);
    }

    [Fact]
    public void EscInASelectWithAFilter_ClearsTheFilter_ThenIsTheShortcut()
    {
        var input = new ScriptedInput("4")
            .Keys(ConsoleKey.Enter)                                                     // quantity 1
            .Key(ScriptedInput.Char('b')).Key(Esc)                                      // filter "b", Esc clears it
            .Key(Esc)                                                                   // no filter: Back
            .Line("q");

        var run = WithEscForBack().Run(input);

        Assert.Equal(["Home/Index", "Quantity/Index", "Home/Index"], run.Routes);
    }
}
