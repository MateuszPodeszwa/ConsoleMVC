using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>
/// Regression tests: controllers with the same name in different namespaces used to replace each other silently.
/// </summary>
public class RouterAmbiguityTests
{
    [Fact]
    public void SameNamedControllers_FailWithAMessageNamingBoth()
    {
        var router = new Router();
        router.DiscoverAll(typeof(RouterAmbiguityTests).Assembly);

        var error = Assert.Throws<InvalidOperationException>(() => router.ResolveController("twin"));

        Assert.Contains("AmbiguousA.Controllers.TwinController", error.Message);
        Assert.Contains("AmbiguousB.Controllers.TwinController", error.Message);
    }

    [Fact]
    public void OtherControllers_StillResolve()
    {
        var router = new Router();
        router.DiscoverAll(typeof(RouterAmbiguityTests).Assembly);

        Assert.IsType<TestApp.Controllers.ShopController>(router.ResolveController("Shop"));
    }

    [Fact]
    public void DiscoveringTheSameAssemblyTwice_IsNotAmbiguous()
    {
        var router = new Router();
        router.DiscoverAll(typeof(RouterAmbiguityTests).Assembly);
        router.DiscoverAll(typeof(RouterAmbiguityTests).Assembly);

        Assert.IsType<TestApp.Controllers.ShopController>(router.ResolveController("Shop"));
    }
}
