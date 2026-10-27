using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Regression tests for action and view resolution (design doc §8, bugs 1 and 2).
/// </summary>
public class RouterResolutionTests
{
    public class CatalogueController : Controller
    {
        public ActionResult Index() => View();

        public ActionResult Search(string term) => View();
        public ActionResult Search(int page) => View();

        public string NotAnAction() => "";

        [NonAction]
        public ActionResult Helper() => View();
    }

    [Fact]
    public void ResolveAction_IsCaseInsensitive()
    {
        var method = new Router().ResolveAction(typeof(CatalogueController), "index");

        Assert.Equal("Index", method.Name);
    }

    [Theory]
    [InlineData("get_ViewData")]
    [InlineData("get_RouteContext")]
    [InlineData("ToString")]
    [InlineData("GetType")]
    [InlineData("GetHashCode")]
    [InlineData("Equals")]
    [InlineData("NotAnAction")]
    [InlineData("Helper")]
    public void ResolveAction_NonActionMethods_AreNotRoutable(string name)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new Router().ResolveAction(typeof(CatalogueController), name));

        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public void ResolveAction_Overloads_ThrowClearError()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new Router().ResolveAction(typeof(CatalogueController), "Search"));

        Assert.Contains("overload", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveView_IsCaseInsensitive()
    {
        var router = new Router();
        router.DiscoverAll(typeof(RouterResolutionTests).Assembly);

        // Defined in ConsoleMVC.Tests.Mvc.TestApp.Views.Shop
        var view = router.ResolveView("shop", "INDEX");

        Assert.IsType<TestApp.Views.Shop.IndexView>(view);
    }

    [Fact]
    public void ResolveController_IsCaseInsensitive()
    {
        var router = new Router();
        router.DiscoverAll(typeof(RouterResolutionTests).Assembly);

        Assert.IsType<TestApp.Controllers.ShopController>(router.ResolveController("SHOP"));
    }
}
