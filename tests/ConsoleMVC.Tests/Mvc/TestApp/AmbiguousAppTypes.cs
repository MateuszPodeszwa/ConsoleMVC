using ConsoleMVC.Mvc;

// Two controllers with the same name in different namespaces, for RouterRegressionTests.

namespace ConsoleMVC.Tests.Mvc.AmbiguousA.Controllers
{
    public sealed class TwinController : Controller
    {
        public ActionResult Index() => View();
    }
}

namespace ConsoleMVC.Tests.Mvc.AmbiguousB.Controllers
{
    public sealed class TwinController : Controller
    {
        public ActionResult Index() => View();
    }
}
