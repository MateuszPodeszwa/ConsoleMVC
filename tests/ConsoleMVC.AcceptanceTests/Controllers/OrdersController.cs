using ConsoleMVC.Mvc;

namespace Acceptance.Controllers;

/// <summary>A list and a details page with route values, for navigation history and Back.</summary>
public class OrdersController : Controller
{
    /// <summary>The list of orders. Example: <c>nav="Orders/Index"</c>.</summary>
    public ActionResult Index() => View();

    /// <summary>One order, chosen by its route value. Example: <c>nav="Orders/Details?id=2"</c>.</summary>
    public ActionResult Details(int id) => View((object)id);
}
