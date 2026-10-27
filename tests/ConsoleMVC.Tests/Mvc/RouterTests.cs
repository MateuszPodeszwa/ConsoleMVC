using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests;

public class RouterTests
{
    // --- Test controllers ---

    public class TestController : Controller
    {
        public ActionResult Index() => View();
        public ActionResult Greet(string name) => View();
        public ActionResult Process(string name, int age) => View();
    }

    [Fact]
    public void ResolveAction_ParameterlessMethod_Succeeds()
    {
        var router = new Router();
        var method = router.ResolveAction(typeof(TestController), "Index");

        Assert.Equal("Index", method.Name);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void ResolveAction_SingleParam_Succeeds()
    {
        var router = new Router();
        var method = router.ResolveAction(typeof(TestController), "Greet");

        Assert.Equal("Greet", method.Name);
        Assert.Single(method.GetParameters());
        Assert.Equal("name", method.GetParameters()[0].Name);
    }

    [Fact]
    public void ResolveAction_MultipleParams_Succeeds()
    {
        var router = new Router();
        var method = router.ResolveAction(typeof(TestController), "Process");

        Assert.Equal("Process", method.Name);
        Assert.Equal(2, method.GetParameters().Length);
    }

    [Fact]
    public void ResolveAction_UnknownAction_Throws()
    {
        var router = new Router();

        Assert.Throws<InvalidOperationException>(() =>
            router.ResolveAction(typeof(TestController), "NonExistent"));
    }
}
