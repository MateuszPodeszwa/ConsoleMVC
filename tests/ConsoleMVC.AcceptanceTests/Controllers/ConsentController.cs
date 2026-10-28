using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>A <c>&lt;checkbox&gt;</c>, for its answers in another language.</summary>
public class ConsentController : Controller
{
    /// <summary>The form. Example: <c>nav="Consent/Index"</c>.</summary>
    public ActionResult Index() => View();

    /// <summary>Shows what was posted.</summary>
    public ActionResult Show(bool agree) => View((object)$"agree={agree}");
}
