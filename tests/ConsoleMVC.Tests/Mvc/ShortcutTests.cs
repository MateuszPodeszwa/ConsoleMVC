using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.KioskApp;
using ConsoleMVC.Tests.Mvc.KioskApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;
using ScriptedInput = ConsoleMVC.Testing.ScriptedInput;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for keyboard shortcuts: <see cref="MvcApplication.MapKey(ConsoleKey, string, string?)"/> (docs/design/shortcuts.md).</summary>
[Collection(TestAppCollection.Name)]
public class ShortcutTests
{
    private static MvcTestHost Host(Action<MvcApplication> shortcuts)
    {
        var host = MvcTestHost.For<KioskController>(builder =>
        {
            builder.DefaultController = "Kiosk";
            builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
        });
        host.ConfigureApplication = shortcuts;
        return host;
    }

    // ------------------------------------------------------------------ KeyGesture

    [Theory]
    [InlineData("F1", ConsoleKey.F1, (ConsoleModifiers)0, "F1")]
    [InlineData("esc", ConsoleKey.Escape, (ConsoleModifiers)0, "Esc")]
    [InlineData("Ctrl+Q", ConsoleKey.Q, ConsoleModifiers.Control, "Ctrl+Q")]
    [InlineData("alt+shift+s", ConsoleKey.S, ConsoleModifiers.Alt | ConsoleModifiers.Shift, "Alt+Shift+S")]
    [InlineData("Ctrl+5", ConsoleKey.D5, ConsoleModifiers.Control, "Ctrl+5")]
    [InlineData("PageUp", ConsoleKey.PageUp, (ConsoleModifiers)0, "PageUp")]
    public void KeyGesture_ParsesAndFormats(string text, ConsoleKey key, ConsoleModifiers modifiers, string formatted)
    {
        var gesture = KeyGesture.Parse(text);

        Assert.Equal(new KeyGesture(key, modifiers), gesture);
        Assert.Equal(formatted, gesture.ToString());
    }

    [Theory]
    [InlineData("Hyper+Q")]
    [InlineData("Ctrl+")]
    [InlineData("Banana")]
    [InlineData("42")]
    public void KeyGesture_RejectsWhatIsNotAKey(string text)
    {
        Assert.Throws<FormatException>(() => KeyGesture.Parse(text));
    }

    // ------------------------------------------------------------------ MapKey

    [Theory]
    [InlineData("Q")]
    [InlineData("Enter")]
    [InlineData("UpArrow")]
    [InlineData("Spacebar")]
    [InlineData("Ctrl+C")]
    [InlineData("Ctrl+D")]
    [InlineData("Ctrl+Q")]
    [InlineData("Ctrl+S")]
    [InlineData("Shift+A")]
    public void MapKey_RefusesKeysNeededForTyping(string gesture)
    {
        using var app = MvcApplication.CreateBuilder([]).Build();

        Assert.Throws<ArgumentException>(() => app.MapKey(KeyGesture.Parse(gesture), NavigationResult.Quit()));
    }

    [Fact]
    public void MapKey_RefusesDuplicates_AndInvalidTargets()
    {
        using var app = MvcApplication.CreateBuilder([]).Build();
        app.MapKey(ConsoleKey.F1, "Help/Index", "Help");

        Assert.Throws<ArgumentException>(() => app.MapKey(ConsoleKey.F1, "Other/Index"));
        Assert.Throws<ArgumentException>(() => app.MapKey(ConsoleKey.F2, "Too/Many/Parts"));
        Assert.Single(app.Shortcuts);
    }

    // ------------------------------------------------------------------ in a running application

    [Fact]
    public void Shortcut_AtAMenu_Navigates()
    {
        var run = Host(app => app.MapKey(ConsoleKey.F2, "Kiosk/Form", "Form"))
            .Run(new ScriptedInput().Keys(ConsoleKey.F2).Lines("Ada", "2"));

        Assert.Equal(["Kiosk/Index", "Kiosk/Form", "Kiosk/Done"], run.Routes);
        Assert.Equal("Hello, Ada (green)\n", run.LastScreen);
    }

    [Fact]
    public void Shortcut_InATextField_GoesBack()
    {
        var run = Host(app => app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back"))
            .Run(new ScriptedInput("1").Keys(ConsoleKey.Escape).Line("q"));

        Assert.Equal(["Kiosk/Index", "Kiosk/Form", "Kiosk/Index"], run.Routes);
    }

    [Fact]
    public void Shortcut_InAnArrowKeyField_Quits()
    {
        var run = Host(app => app.MapKey(KeyGesture.Parse("Ctrl+X"), NavigationResult.Quit()))
            .Run(new ScriptedInput("1", "Ada").Keys(ConsoleKey.DownArrow).Key(new ConsoleKeyInfo('\u0018', ConsoleKey.X, false, false, true)));

        Assert.Equal(["Kiosk/Index", "Kiosk/Form"], run.Routes);
        Assert.Equal(0, run.RemainingInput);
    }

    [Fact]
    public void WithShortcuts_LinesCanBeTypedKeyByKey()
    {
        var input = new ScriptedInput("1")
            .Key(ScriptedInput.Char('A')).Key(ScriptedInput.Char('x')).Keys(ConsoleKey.Backspace)
            .Key(ScriptedInput.Char('d')).Key(ScriptedInput.Char('a')).Keys(ConsoleKey.Enter)
            .Line("3");

        var run = Host(app => app.MapKey(ConsoleKey.F1, "Kiosk/Index")).Run(input);

        Assert.Equal("Hello, Ada (blue)\n", run.LastScreen);
    }

    [Fact]
    public void WithoutShortcuts_TheInputIsNotWrapped()
    {
        // A key where a line is read fails as before, rather than being edited
        Assert.Throws<InvalidOperationException>(() =>
            Host(_ => { }).Run(new ScriptedInput().Keys(ConsoleKey.F2)));
    }

    // ------------------------------------------------------------------ LineEditor and <shortcuts />

    [Fact]
    public void LineEditor_EditsAndEchoes()
    {
        var echo = new StringWriter { NewLine = "\n" };
        var editor = new LineEditor(echo);
        string? line = null;
        var keys = new[]
        {
            Ch('a'), Ch('c'), Key(ConsoleKey.LeftArrow), Ch('b'), Key(ConsoleKey.End), Ch('d'),
            Key(ConsoleKey.Home), Key(ConsoleKey.Delete), Key(ConsoleKey.Enter)
        };

        foreach (var key in keys)
        {
            if (editor.Process(key, out line))
                break;
        }

        Assert.Equal("bcd", line);
        Assert.Equal("ac\bbc\bcd\b\b\b\bbcd \b\b\b\b\n", echo.ToString());

        static ConsoleKeyInfo Ch(char c) => new(c, ConsoleKey.A, false, false, false);
        static ConsoleKeyInfo Key(ConsoleKey k) => new(k == ConsoleKey.Enter ? '\r' : '\0', k, false, false, false);
    }

    [Fact]
    public void ShortcutsElement_ShowsWhatFits()
    {
        var element = new ShortcutsElement { Items = [new ShortcutHint("F1", "Help"), new ShortcutHint("Esc", "Back"), new ShortcutHint("Ctrl+X", "Quit")] };

        Assert.Equal("F1 Help  Esc Back  Ctrl+X Quit", element.Layout(60)[0].ToPlainText());
        Assert.Equal("F1 Help  Esc Back", element.Layout(20)[0].ToPlainText());
        Assert.Empty(new ShortcutsElement().Layout(60));
    }

    [Fact]
    public void Views_GetTheDescribedShortcuts()
    {
        var run = Host(app =>
        {
            app.MapKey(ConsoleKey.F1, "Kiosk/Index", "Home");
            app.MapKey(ConsoleKey.F2, "Kiosk/Form"); // no description: not listed
        }).Run("q");

        Assert.Contains("\nF1 Home\n", run.Screens[0]);
        Assert.DoesNotContain("F2", run.Screens[0]);
    }
}
