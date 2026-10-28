using ConsoleMVC.Mvc;

namespace Acceptance.Areas.Admin.Controllers;

/// <summary>The Admin area's own Home, which hides the application's from inside the area.</summary>
public class HomeController : Controller
{
    /// <summary>The area's start page. Example: <c>nav="Admin/Home/Index"</c>.</summary>
    public ActionResult Index() => View();
}
