using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>A <c>&lt;number&gt;</c> and a <c>&lt;select filter&gt;</c>, for keys that fields and shortcuts both use.</summary>
public class QuantityController : Controller
{
    /// <summary>The form. Example: <c>nav="Quantity/Index"</c>.</summary>
    public ActionResult Index() => View();

    /// <summary>Shows what was posted.</summary>
    public ActionResult Show(int quantity, string colour) => View((object)$"quantity={quantity} colour={colour}");
}
