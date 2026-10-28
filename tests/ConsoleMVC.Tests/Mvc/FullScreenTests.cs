using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.KioskApp;
using ConsoleMVC.Tests.Mvc.KioskApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for <see cref="MvcApplicationBuilder.FullScreen"/> (docs/design/full-screen.md).</summary>
[Collection(TestAppCollection.Name)]
public class FullScreenTests
{
    /// <summary>A screen that records switches to and from the alternate screen.</summary>
    private sealed class AlternateScreen : IRenderTarget
    {
        public List<bool> Switches { get; } = [];

        public bool Supported { get; init; } = true;

        public void Write(RenderedLine line) { }

        public void WriteInline(RenderedLine line) { }

        public void Clear() { }

        public void Flush() { }

        public bool TrySetAlternateScreen(bool on)
        {
            if (Supported)
                Switches.Add(on);
            return Supported;
        }
    }

    private static MvcApplication App(AlternateScreen screen, bool fullScreen)
    {
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(KioskController).Assembly;
        builder.DefaultController = "Kiosk";
        builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
        builder.Renderer = new MarkupRenderer(screen, () => 40);
        builder.Input = new Rendering.ScriptedInput("q");
        builder.FullScreen = fullScreen;
        return builder.Build();
    }

    [Fact]
    public void FullScreen_EntersTheAlternateScreen_AndLeavesIt()
    {
        var screen = new AlternateScreen();
        using var app = App(screen, fullScreen: true);

        app.Run();

        Assert.Equal([true, false], screen.Switches);
    }

    [Fact]
    public void FullScreen_LeavesTheAlternateScreen_WhenTheApplicationFails()
    {
        var screen = new AlternateScreen();
        using var app = App(screen, fullScreen: true);
        app.Use((_, _) => throw new InvalidOperationException("boom"));

        Assert.Throws<InvalidOperationException>(() => app.Run());
        Assert.Equal([true, false], screen.Switches);
    }

    [Fact]
    public void FullScreen_IsOff_ByDefault_AndIgnoredWithoutAnAlternateScreen()
    {
        var off = new AlternateScreen();
        using (var app = App(off, fullScreen: false))
            app.Run();
        Assert.Empty(off.Switches);

        var unsupported = new AlternateScreen { Supported = false };
        using (var app = App(unsupported, fullScreen: true))
            app.Run();
        Assert.Empty(unsupported.Switches);
    }
}
