using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.TestApp;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Integration tests that run the real application loop against the miniature app in
/// <c>TestApp/TestAppTypes.cs</c> (design doc §8, bugs 1, 3, 4, 5 and 6).
/// </summary>
[Collection(TestAppCollection.Name)]
public class MvcApplicationTests
{
    /// <summary>A renderer that records calls instead of touching the console.</summary>
    private sealed class RecordingRenderer : IMarkupRenderer
    {
        public int ClearCount { get; private set; }
        public List<Element> Rendered { get; } = [];
        public int Width => 80;
        public void Render(Element element) => Rendered.Add(element);
        public void Clear() => ClearCount++;
    }

    private static (MvcApplication App, RecordingRenderer Renderer) CreateApp(string action = "Index")
    {
        Script.Reset();
        var renderer = new RecordingRenderer();
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(MvcApplicationTests).Assembly;
        builder.DefaultController = "Shop";
        builder.DefaultAction = action;
        builder.Renderer = renderer;
        return (builder.Build(), renderer);
    }

    [Fact]
    public void Run_RendersDefaultRoute_ThenQuits()
    {
        var (app, renderer) = CreateApp();

        app.Run();

        Assert.Equal(["action:Index", "view:Index"], Script.Log);
        Assert.Equal(1, renderer.ClearCount);
    }

    [Fact]
    public void Run_NavigationIsCaseInsensitive()
    {
        var (app, _) = CreateApp();
        var first = true;
        Script.Next["Index"] = () =>
        {
            if (!first) return NavigationResult.Quit();
            first = false;
            return NavigationResult.To("shop", "basket");
        };

        app.Run();

        Assert.Equal(["action:Index", "view:Index", "action:Basket", "view:Basket(2)"], Script.Log);
    }

    [Fact]
    public void Run_PostsFormData_ThenFollowsRedirect()
    {
        var (app, _) = CreateApp("Basket");
        var posted = false;
        Script.Next["Basket"] = () =>
        {
            posted = true;
            return NavigationResult.ToAction("Order", new Dictionary<string, string> { ["quantity"] = "3" });
        };
        Script.Next["Index"] = () => NavigationResult.Quit();

        app.Run();

        Assert.True(posted);
        Assert.Equal(["action:Basket", "view:Basket(2)", "action:Order(3)", "action:Index", "view:Index"], Script.Log);
    }

    [Fact]
    public void Run_ClearScreenCanBeDisabled()
    {
        var (_, renderer) = CreateApp();
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(MvcApplicationTests).Assembly;
        builder.DefaultController = "Shop";
        builder.Renderer = renderer;
        builder.ClearScreenBetweenViews = false;

        builder.Build().Run();

        Assert.Equal(0, renderer.ClearCount);
    }

    [Fact]
    public void Run_UnsupportedActionResult_ThrowsInsteadOfLoopingForever()
    {
        var (app, _) = CreateApp("Unsupported");

        var error = Assert.Throws<InvalidOperationException>(app.Run);

        Assert.Contains(nameof(UnsupportedResult), error.Message);
    }

    [Fact]
    public void Run_EndlessRedirects_AreDetected()
    {
        var (app, _) = CreateApp("Loop");

        var error = Assert.Throws<InvalidOperationException>(app.Run);

        Assert.Contains("redirect", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_ActionException_SurfacesOriginalException()
    {
        var (app, _) = CreateApp("Broken");

        var error = Assert.Throws<ApplicationException>(app.Run);

        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public void Run_WrongModelType_ReportsViewAndTypes()
    {
        var (app, _) = CreateApp("WrongModel");

        var error = Assert.Throws<InvalidOperationException>(app.Run);

        Assert.Contains("WrongModelView", error.Message);
        Assert.Contains(nameof(OrderModel), error.Message);
        Assert.Contains("String", error.Message);
    }

    [Fact]
    public void Run_GivesViewsTheConfiguredRenderer()
    {
        var (app, renderer) = CreateApp("Rendering");

        app.Run();

        Assert.IsType<LineBreakElement>(Assert.Single(renderer.Rendered));
    }

    [Fact]
    public void View_UsedStandalone_DefaultsToConsoleRenderer()
    {
        Assert.NotNull(new TestApp.Views.Shop.IndexView().Renderer);
    }
}
