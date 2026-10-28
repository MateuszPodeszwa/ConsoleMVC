using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>The start page: a menu leading to every screen the acceptance tests use.</summary>
public class HomeController : Controller
{
    /// <summary>Shows the menu. Example: the first screen of every run.</summary>
    public ActionResult Index() => View();
}
