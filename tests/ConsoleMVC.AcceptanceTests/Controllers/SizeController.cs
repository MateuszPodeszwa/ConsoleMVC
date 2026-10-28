using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>A side-by-side <c>&lt;radio&gt;</c>, for clicking its choices and wrapping long ones.</summary>
public class SizeController : Controller
{
    /// <summary>The form. Example: <c>nav="Size/Index"</c>.</summary>
    public ActionResult Index() => View();

    /// <summary>Shows what was posted.</summary>
    public ActionResult Show(string size) => View((object)$"size={size}");
}
