using ConsoleMVC.Mvc;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.KioskApp;
using ConsoleMVC.Tests.Mvc.KioskApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for <see cref="MvcApplicationBuilder.Mouse"/> in an application (docs/design/mouse.md).</summary>
[Collection(TestAppCollection.Name)]
public class MouseAppTests
{
    private static MvcTestHost Host(bool mouse = true) => MvcTestHost.For<KioskController>(builder =>
    {
        builder.DefaultController = "Kiosk";
        builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
        builder.Mouse = mouse;
    });

    [Fact]
    public void ClickingAMenuOption_ChoosesIt()
    {
        // Welcome is row 0; the options [1] Form, [l] Loop and [q] Quit are rows 1 to 3
        var run = Host().Run(new ScriptedInput().Click(2, 1).Line("Ada").Keys(ConsoleKey.Enter));

        Assert.Equal(["Kiosk/Index", "Kiosk/Form", "Kiosk/Done"], run.Routes);
        Assert.Contains("Hello, Ada (red)", run.LastScreen);
    }

    [Fact]
    public void Clicks_WorkWithShortcuts_AndTypingStillWorks()
    {
        var host = Host();
        host.ConfigureApplication = app => app.MapKey(new KeyGesture(ConsoleKey.F2), NavigationResult.Quit(), "Quit");

        // The shortcuts line is row 1 now, so [q] Quit is row 4; "z" typed first is replaced by the click
        var run = host.Run(new ScriptedInput().Key(ScriptedInput.Char('z')).Click(2, 4));

        Assert.Equal(["Kiosk/Index"], run.Routes);
        Assert.Contains("F2 Quit", run.Screens[0]);
    }

    [Fact]
    public void WithoutMouseSupport_ClicksAreNotDecoded()
    {
        var host = Host(mouse: false);

        Assert.Throws<InvalidOperationException>(() => host.Run(new ScriptedInput().Click(2, 1)));
    }

    [Fact]
    public void Mouse_CanBeChosenInTheSettings()
    {
        var host = MvcTestHost.For<KioskController>(builder =>
        {
            builder.DefaultController = "Kiosk";
            builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
            builder.Configuration[MvcApplicationBuilder.MouseSetting] = "true";
        });

        var run = host.Run(new ScriptedInput().Click(2, 1).Line("Ada").Keys(ConsoleKey.Enter));

        Assert.Equal("Kiosk/Done", run.Routes[^1]);
    }
}
