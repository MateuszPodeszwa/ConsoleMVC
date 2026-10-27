using ConsoleMVC.Mvc;

// Controllers and views for NavigationHistoryTests.

namespace ConsoleMVC.Tests.Mvc.HistoryApp
{
    /// <summary>What the history tests observe.</summary>
    public static class HistoryScript
    {
        /// <summary>The navigation each successive screen returns.</summary>
        public static Queue<NavigationResult> Steps { get; } = new();

        /// <summary>The screens shown, with the breadcrumbs each one saw.</summary>
        public static List<string> Log { get; } = [];

        public static void Reset(params NavigationResult[] steps)
        {
            Steps.Clear();
            Log.Clear();
            foreach (var step in steps)
                Steps.Enqueue(step);
        }

        public static NavigationResult Next(ConsoleView view, string name)
        {
            Log.Add($"{name} [{string.Join(" > ", view.Breadcrumbs)}]");
            return Steps.Count > 0 ? Steps.Dequeue() : NavigationResult.Quit();
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.HistoryApp.Controllers
{
    public sealed class TrailController : Controller
    {
        public ActionResult Index() => View();

        public ActionResult List() => View();

        public ActionResult Details(int id) => View((object)id.ToString());

        public ActionResult Jump() => RedirectToAction("Details");
    }
}

namespace ConsoleMVC.Tests.Mvc.HistoryApp.Views.Trail
{
    using ConsoleMVC.Tests.Mvc.HistoryApp;

    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => HistoryScript.Next(this, "Index");
    }

    public sealed class ListView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => HistoryScript.Next(this, "List");
    }

    public sealed class DetailsView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => HistoryScript.Next(this, $"Details({Model})");
    }
}

namespace ConsoleMVC.Tests.Mvc.HistoryApp.Controllers
{
    public sealed class ShelfController : Controller
    {
        public ActionResult Index() => View();

        public ActionResult Item(int id, string? name, [System.ComponentModel.DataAnnotations.Range(1, 5)] int stars = 1)
        {
            HistoryApp.HistoryScript.Log.Add($"item:{id}:{name}:{stars}:{ModelState.IsValid}");
            return View();
        }

        public ActionResult Forward(int id) => RedirectToAction("Item", new { id = id + 1, name = "a & b" });

        public ActionResult Across() => RedirectToAction("Item", "Shelf", new Dictionary<string, string> { ["id"] = "9" });
    }
}

namespace ConsoleMVC.Tests.Mvc.HistoryApp.Views.Shelf
{
    using ConsoleMVC.Tests.Mvc.HistoryApp;

    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => HistoryScript.Next(this, "Shelf");
    }

    public sealed class ItemView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model) => HistoryScript.Next(this, "Item");
    }
}
