using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.DiApp;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for hot reload support: after <c>dotnet watch</c> applies a change, the next screen rediscovers controllers
/// and views, so new pages are routable without restarting.
/// </summary>
[Collection(TestAppCollection.Name)]
public class HotReloadTests
{
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    private static MvcApplication CreateApp(int screens)
    {
        DiScript.Reset(screens);
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(HotReloadTests).Assembly;
        builder.DefaultController = "Greeting";
        builder.Renderer = new NullRenderer();
        builder.Services.AddSingleton<IGreeter, Greeter>();
        builder.Services.AddScoped<UnitOfWork>();
        return builder.Build();
    }

    [Fact]
    public void AnAppliedUpdate_RediscoversControllersAndViews_BeforeTheNextScreen()
    {
        using var app = CreateApp(screens: 3);
        var cycles = 0;
        app.Use((context, next) =>
        {
            if (++cycles == 1)
                HotReloadHandler.UpdateApplication(null); // what the runtime calls after dotnet watch applies an edit

            return next(context);
        });

        app.Run();

        Assert.Equal(2, app.DiscoveryCount); // at start, then once after the update
    }

    [Fact]
    public void WithoutUpdates_DiscoveryRunsOnce()
    {
        using var app = CreateApp(screens: 3);

        app.Run();

        Assert.Equal(1, app.DiscoveryCount);
    }

    [Fact]
    public void UpdatesAfterTheRunEnds_AreIgnored()
    {
        using var app = CreateApp(screens: 1);
        app.Run();

        HotReloadHandler.UpdateApplication([typeof(HotReloadTests)]);
        HotReloadHandler.ClearCache(null);

        Assert.Equal(1, app.DiscoveryCount);
    }
}
