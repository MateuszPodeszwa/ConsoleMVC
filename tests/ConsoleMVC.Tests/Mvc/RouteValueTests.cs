using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.HistoryApp;
using ConsoleMVC.Tests.Mvc.TestApp;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for route values: <c>nav="Shelf/Item?id=7"</c>, <see cref="NavigationResult.To(string)"/> and
/// <c>RedirectToAction(action, routeValues)</c> (docs/design/route-values.md).
/// </summary>
[Collection(TestAppCollection.Name)]
public class RouteValueTests
{
    private sealed class NullRenderer : IMarkupRenderer
    {
        public int Width => 80;
        public void Render(Element element) { }
        public void WritePrompt(string text, TextStyle style) { }
        public void Clear() { }
    }

    private static void Run(params NavigationResult[] steps)
    {
        HistoryScript.Reset(steps);
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(RouteValueTests).Assembly;
        builder.DefaultController = "Shelf";
        builder.Renderer = new NullRenderer();
        using var app = builder.Build();
        app.Run();
    }

    private static List<string> Items => HistoryScript.Log.Where(l => l.StartsWith("item:", StringComparison.Ordinal)).ToList();

    [Fact]
    public void RouteValues_BindToActionParameters()
    {
        Run(NavigationResult.To("Shelf/Item?id=7&name=Tea%20pot"));

        Assert.Equal(["item:7:Tea pot:1:True"], Items);
    }

    [Fact]
    public void RouteValues_DoNotTriggerValidation_ButReportConversionErrors()
    {
        Run(NavigationResult.To("Item?id=x&stars=9"));

        // stars=9 is outside [Range(1, 5)], but only posted forms are validated; "x" is not an int
        Assert.Equal(["item:0::9:False"], Items);
    }

    [Fact]
    public void FormData_WinsOverRouteValues()
    {
        Run(new NavigationResult { Action = "Item", RouteValues = new Dictionary<string, string> { ["id"] = "1" }, FormData = new() { ["id"] = "2" } });

        Assert.Equal(["item:2::1:True"], Items);
    }

    [Fact]
    public void RedirectToAction_CarriesRouteValues_FromAnObjectOrADictionary()
    {
        Run(NavigationResult.To("Shelf/Forward?id=4"), NavigationResult.ToAction("Across"));

        Assert.Equal(["item:5:a & b:1:True", "item:9::1:True"], Items);
    }

    [Fact]
    public void Back_ReturnsToTheSameRecord()
    {
        Run(NavigationResult.To("Item?id=3"), NavigationResult.ToAction("Index"), NavigationResult.Back());

        Assert.Equal(["item:3::1:True", "item:3::1:True"], Items);
    }

    [Fact]
    public void MenuTargets_MayCarryRouteValues()
    {
        var menu = new MenuElement();
        var option = new MenuOptionElement { Key = "1", Target = "Shelf/Item?id=12&name=a%26b" };
        option.AppendText("Open");
        menu.Add(option);

        var navigation = new Rendering.MenuHost(menu, new Rendering.ScriptedInput("1")).RenderInternal(null);

        Assert.Equal(("Shelf", "Item"), (navigation.Controller, navigation.Action));
        Assert.Equal("12", navigation.RouteValues!["id"]);
        Assert.Equal("a&b", navigation.RouteValues["name"]);
    }

    [Theory]
    [InlineData("Shelf/Item?", "Shelf", "Item", 0)]
    [InlineData("Item?flag", "", "Item", 1)]
    [InlineData("Item?a=1&&b=2", "", "Item", 2)]
    public void Targets_ParseLeniently(string target, string controller, string action, int values)
    {
        var navigation = NavigationResult.To(target);

        Assert.Equal(controller.Length == 0 ? null : controller, navigation.Controller);
        Assert.Equal(action, navigation.Action);
        Assert.Equal(values, navigation.RouteValues?.Count ?? 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b/c/d")]
    [InlineData("/Item")]
    public void InvalidTargets_Throw(string target)
    {
        Assert.Throws<ArgumentException>(() => NavigationResult.To(target));
    }

    [Fact]
    public void RouteValuesFromObjects_UseTheInvariantCulture()
    {
        var values = RouteValues.From(new { price = 1.5m, when = new DateOnly(2026, 10, 27), ok = true, none = (string?)null });

        Assert.Equal("1.5", values["price"]);
        Assert.Equal("2026-10-27", values["when"]);
        Assert.Equal("true", values["ok"]);
        Assert.False(values.ContainsKey("none"));
    }
}
