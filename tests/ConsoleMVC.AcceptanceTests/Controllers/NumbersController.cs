using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>Binds typed numbers to simple parameters, as docs/guides/docs/controllers-and-forms.md describes.</summary>
public class NumbersController : Controller
{
    /// <summary>The form. Example: <c>nav="Numbers/Index"</c>.</summary>
    public ActionResult Index() => View();

    /// <summary>Shows what was bound and any binding errors.</summary>
    public ActionResult Show(int count, double ratio, Half small, long big) =>
        View((object)$"count={count} ratio={ratio.ToString(System.Globalization.CultureInfo.InvariantCulture)} small={small} big={big} errors={ModelState.ErrorCount}");
}
