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
}
