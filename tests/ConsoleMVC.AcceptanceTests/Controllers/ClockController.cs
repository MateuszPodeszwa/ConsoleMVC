using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>A page that refreshes every second, for <c>@refresh</c>.</summary>
public class ClockController : Controller
{
    private static int _visits;

    /// <summary>The page, numbered each time it is drawn. Example: <c>nav="Clock/Index"</c>.</summary>
    public ActionResult Index() => View((object)Interlocked.Increment(ref _visits));
}
