using ConsoleMVC.Mvc;

// A miniature application used by the MvcApplication integration tests. The namespaces mirror
// what a real app (and the source generator) produces: *.Controllers and *.Views.{Controller}.

namespace ConsoleMVC.Tests.Mvc.TestApp
{
    /// <summary>
    /// Records what happened during a test run. The integration tests run sequentially within
    /// one test class, so a static script is safe here.
    /// </summary>
    public static class Script
    {
        /// <summary>Ordered log of visited actions and views.</summary>
        public static List<string> Log { get; } = [];

        /// <summary>Navigation each view should return, keyed by view name.</summary>
        public static Dictionary<string, Func<NavigationResult>> Next { get; } = new();

        /// <summary>Resets the script between tests.</summary>
        public static void Reset()
        {
            Log.Clear();
            Next.Clear();
        }

        /// <summary>Returns the scripted navigation for a view, or Quit.</summary>
        public static NavigationResult NavigationFor(string view) =>
            Next.TryGetValue(view, out var next) ? next() : NavigationResult.Quit();
    }

    /// <summary>A custom result type the framework does not know how to process.</summary>
    public sealed class UnsupportedResult : ActionResult
    {
    }

    public sealed record OrderModel(int Quantity);

    public sealed class CheckoutForm
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Email { get; set; } = "";

        [System.ComponentModel.DataAnnotations.Range(1, 10)]
        public int Quantity { get; set; } = 1;
    }

    /// <summary>
    /// Test classes that share <see cref="Script"/> join this collection so xUnit runs them sequentially.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class TestAppCollection
    {
        public const string Name = "TestApp";
    }
}

namespace ConsoleMVC.Tests.Mvc.TestApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.TestApp;

    public class ShopController : Controller
    {
        public ActionResult Index()
        {
            Script.Log.Add("action:Index");
            return View();
        }

        public ActionResult Basket()
        {
            Script.Log.Add("action:Basket");
            return View(new OrderModel(2));
        }

        public ActionResult Order(int quantity)
        {
            Script.Log.Add($"action:Order({quantity})");
            return RedirectToAction("Index");
        }

        public ActionResult Loop() => RedirectToAction("Loop");

        public ActionResult Unsupported() => new UnsupportedResult();

        public ActionResult Broken() => throw new ApplicationException("boom");

        public ActionResult WrongModel() => View((object)"not an OrderModel");

        public ActionResult Rendering() => View();

        public ActionResult Ask() => View();

        public ActionResult Start()
        {
            Script.Log.Add("action:Start");
            return View();
        }

        public ActionResult Checkout(CheckoutForm form)
        {
            if (!ModelState.IsValid)
            {
                Script.Log.Add($"action:Checkout(invalid:{ModelState.ErrorCount})");
                return View("Basket", new OrderModel(0));
            }

            Script.Log.Add("action:Checkout(valid)");
            return RedirectToAction("Start");
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.TestApp.Views.Shop
{
    using ConsoleMVC.Tests.Mvc.TestApp;

    public class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            Script.Log.Add("view:Index");
            return Script.NavigationFor("Index");
        }
    }

    public class StartView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            var next = Script.NavigationFor("Start");
            Script.Next["Start"] = NavigationResult.Quit; // only post once
            return next;
        }
    }

    public class BasketView : ConsoleView<OrderModel>
    {
        public override NavigationResult Render(OrderModel Model)
        {
            Script.Log.Add($"view:Basket({Model.Quantity})");
            if (!ModelState.IsValid)
                Script.Log.Add($"view:Basket errors:{ModelState.ErrorCount}");
            return Script.NavigationFor("Basket");
        }
    }

    public class WrongModelView : ConsoleView<OrderModel>
    {
        public override NavigationResult Render(OrderModel Model) => NavigationResult.Quit();
    }

    public class RenderingView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            Renderer.Render(new ConsoleMVC.Rendering.LineBreakElement());
            return NavigationResult.Quit();
        }
    }

    public class AskView : ConsoleView<object>
    {
        /// <summary>The value read by the last run, for assertions.</summary>
        public static string? Answer;

        public override NavigationResult Render(object Model)
        {
            Answer = ReadInput(new ConsoleMVC.Rendering.InputElement { Name = "Answer" });
            return NavigationResult.Quit();
        }
    }

    public class CountView : ConsoleView<int>
    {
        public override NavigationResult Render(int Model) => NavigationResult.Quit();
    }
}
