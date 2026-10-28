namespace Acceptance;

/// <summary>
/// Navigation history, Back and breadcrumbs, as docs/guides/docs/controllers-and-forms.md describes them: a screen is
/// its controller, action and route values.
/// </summary>
public class NavigationAcceptanceTests
{
    [Fact]
    public void TheSameActionWithOtherRouteValues_IsAnotherScreen_AndBackReturnsToIt()
    {
        var run = App.Host().Run("3", "2", "n", "b", "q");   // Orders, order 2, order 42, Back

        Assert.Contains("Home › Orders › Details › Details", run.Screens[3]);
        Assert.Contains("Order 2", run.LastScreen);
        Assert.Contains("Home › Orders › Details", run.LastScreen);
    }

    [Fact]
    public void GoingToAScreenAlreadyInTheHistory_ReturnsTheHistoryToIt()
    {
        var run = App.Host().Run("3", "2", "h", "3", "b", "q");   // Orders, order 2, Home, Orders, Back

        Assert.DoesNotContain("Details", run.Screens[3]);           // Home again: the trail is just "Home"
        Assert.Contains("Home › Orders", run.Screens[4]);
        Assert.Equal("Home/Index", run.Routes[^1]);                 // Back from Orders is Home, not order 2
    }

    [Fact]
    public void BackOnTheFirstScreen_ShowsItAgain()
    {
        var run = App.Host().Run("3", "b", "b", "q");

        Assert.Equal(["Home/Index", "Orders/Index", "Home/Index"], run.Routes);
    }
}
