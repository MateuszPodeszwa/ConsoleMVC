using ConsoleMVC.Mvc;
using ConsoleMVC.Tests.Mvc.TestApp;

// Controllers, views and services for the dependency injection tests (docs/design/dependency-injection.md).

namespace ConsoleMVC.Tests.Mvc.DiApp
{
    /// <summary>A singleton service injected into controllers and views.</summary>
    public interface IGreeter
    {
        string Greet(string name);
    }

    /// <summary>The default greeter.</summary>
    public sealed class Greeter : IGreeter
    {
        public string Greet(string name) => $"Hello, {name}";
    }

    /// <summary>A scoped service that records the identity of each instance and whether it was disposed.</summary>
    public sealed class UnitOfWork : IDisposable
    {
        private static int _counter;

        /// <summary>Gets the sequence number of this instance.</summary>
        public int Id { get; } = Interlocked.Increment(ref _counter);

        /// <summary>Gets a value indicating whether the scope disposed this instance.</summary>
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    /// <summary>A service that is deliberately never registered.</summary>
    public interface IMissingService;

    /// <summary>Everything the DI tests observe during a run.</summary>
    public static class DiScript
    {
        /// <summary>Ordered log of what controllers and views saw.</summary>
        public static List<string> Log { get; } = [];

        /// <summary>Every unit of work handed to a controller or view.</summary>
        public static List<UnitOfWork> Units { get; } = [];

        /// <summary>Controllers that were disposed.</summary>
        public static List<string> Disposed { get; } = [];

        /// <summary>How many views to show before quitting.</summary>
        public static int ScreensLeft { get; set; }

        public static void Reset(int screens = 1)
        {
            Log.Clear();
            Units.Clear();
            Disposed.Clear();
            ScreensLeft = screens;
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.DiApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.DiApp;

    /// <summary>A controller that receives its services through the constructor.</summary>
    public sealed class GreetingController(IGreeter greeter, UnitOfWork unit) : Controller, IDisposable
    {
        public ActionResult Index()
        {
            DiScript.Units.Add(unit);
            DiScript.Log.Add("action:" + greeter.Greet("Ada"));
            return View();
        }

        public ActionResult Missing() => View();

        public ActionResult Bounce() => RedirectToAction("Index");

        public void Dispose() => DiScript.Disposed.Add(nameof(GreetingController));
    }
}

namespace ConsoleMVC.Tests.Mvc.DiApp.Views.Greeting
{
    using ConsoleMVC.Tests.Mvc.DiApp;

    /// <summary>A view with properties injected the way <c>@inject</c> generates them.</summary>
    public sealed class IndexView : ConsoleView<object>
    {
        [Inject]
        public IGreeter Greeter { get; private set; } = default!;

        [Inject]
        public UnitOfWork Unit { get; private set; } = default!;

        public override NavigationResult Render(object Model)
        {
            DiScript.Units.Add(Unit);
            DiScript.Log.Add("view:" + Greeter.Greet("Grace"));
            return --DiScript.ScreensLeft > 0 ? NavigationResult.ToAction("Index") : NavigationResult.Quit();
        }
    }

    /// <summary>A view injecting a service nobody registered.</summary>
    public sealed class MissingView : ConsoleView<object>
    {
        [Inject]
        public IMissingService Service { get; private set; } = default!;

        public override NavigationResult Render(object Model) => NavigationResult.Quit();
    }
}
