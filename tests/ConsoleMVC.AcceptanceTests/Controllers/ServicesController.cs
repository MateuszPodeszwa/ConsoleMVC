using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>A view that injects a service nobody registered, for the message a developer sees.</summary>
public class ServicesController : Controller
{
    /// <summary>The view with the missing service. Example: <c>nav="Services/Index"</c>.</summary>
    public ActionResult Index() => View();
}
