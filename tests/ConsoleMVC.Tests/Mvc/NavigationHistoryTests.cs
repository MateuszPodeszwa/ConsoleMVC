using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.HistoryApp;
using ConsoleMVC.Tests.Mvc.TestApp;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Tests for navigation history: <see cref="NavigationResult.Back"/>, <c>&lt;option back&gt;</c> and breadcrumbs
/// (docs/design/navigation-history.md).
/// </summary>
[Collection(TestAppCollection.Name)]
public class NavigationHistoryTests
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
        builder.ApplicationAssembly = typeof(NavigationHistoryTests).Assembly;
        builder.DefaultController = "Trail";
        builder.Renderer = new NullRenderer();
        using var app = builder.Build();
        app.Run();
    }

    [Fact]
    public void Breadcrumbs_FollowTheScreensShown()
    {
        Run(NavigationResult.ToAction("List"), NavigationResult.ToAction("Details", new() { ["id"] = "7" }));

        Assert.Equal(["Index [Trail]", "List [Trail > List]", "Details(7) [Trail > List > Details]"], HistoryScript.Log);
    }

    [Fact]
    public void Back_ReturnsToThePreviousScreen_AndShortensTheTrail()
    {
        Run(NavigationResult.ToAction("List"), NavigationResult.ToAction("Details"), NavigationResult.Back(), NavigationResult.Back());

        Assert.Equal(
            ["Index [Trail]", "List [Trail > List]", "Details(0) [Trail > List > Details]", "List [Trail > List]", "Index [Trail]"],
            HistoryScript.Log);
    }

    [Fact]
    public void Back_OnTheFirstScreen_ShowsItAgain()
    {
        Run(NavigationResult.Back());

        Assert.Equal(["Index [Trail]", "Index [Trail]"], HistoryScript.Log);
    }

    [Fact]
    public void ShowingTheSameScreenAgain_DoesNotGrowTheTrail()
    {
        // For example a form shown again after validation fails
        Run(NavigationResult.ToAction("List"), NavigationResult.ToAction("list"), NavigationResult.Back());

        Assert.Equal(["Index [Trail]", "List [Trail > List]", "List [Trail > List]", "Index [Trail]"], HistoryScript.Log);
    }

    [Fact]
    public void Redirects_AreNotScreens()
    {
        Run(NavigationResult.ToAction("Jump"), NavigationResult.Back());

        Assert.Equal(["Index [Trail]", "Details(0) [Trail > Details]", "Index [Trail]"], HistoryScript.Log);
    }

    [Fact]
    public void History_IsCapped()
    {
        var steps = Enumerable.Range(0, MvcApplication.MaxHistory + 5)
            .Select(i => NavigationResult.ToAction(i % 2 == 0 ? "List" : "Details"))
            .ToArray();

        Run(steps);

        Assert.Equal(MvcApplication.MaxHistory, HistoryScript.Log[^1].Split(" > ").Length);
    }

    [Fact]
    public void BackOption_InAMenu_ReturnsBack()
    {
        var menu = new MenuElement();
        var option = new MenuOptionElement { Key = "b", Back = true };
        option.AppendText("Back");
        menu.Add(option);
        var view = new Rendering.MenuHost(menu, new Rendering.ScriptedInput("b"));

        Assert.True(view.RenderInternal(null).IsBack);
    }

    [Fact]
    public void BreadcrumbsElement_JoinsTheTrail_AndTruncatesFromTheStart()
    {
        var crumbs = new BreadcrumbsElement { Items = ["Home", "Orders", "Order 42"] };

        Assert.Equal("Home › Orders › Order 42", crumbs.Layout(40).Single().ToPlainText());
        Assert.Equal("… › Orders › Order 42", crumbs.Layout(22).Single().ToPlainText());
        Assert.Empty(new BreadcrumbsElement().Layout(40));
        Assert.All(crumbs.Layout(5), line => Assert.True(line.Width <= 5));
    }
}
