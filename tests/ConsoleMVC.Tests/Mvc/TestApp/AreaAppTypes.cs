using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

// Controllers and views in areas (docs/design/areas-and-modules.md).

namespace ConsoleMVC.Tests.Mvc.AreaApp
{
    /// <summary>Shared view helpers: a line of text and a menu built from (key, target) pairs.</summary>
    internal static class Screen
    {
        public static void Say(IMarkupRenderer renderer, string text)
        {
            var element = new TextElement();
            element.AppendText(text);
            renderer.Render(element);
        }

        public static MenuElement Menu(params (string Key, string Target)[] options)
        {
            var menu = new MenuElement();
            foreach (var (key, target) in options)
            {
                var option = new MenuOptionElement { Key = key, Target = target };
                option.AppendText(target);
                menu.Add(option);
            }

            var quit = new MenuOptionElement { Key = "q", Quit = true };
            quit.AppendText("Quit");
            menu.Add(quit);
            return menu;
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.AreaApp.Controllers
{
    /// <summary>The root controller; the Admin area has one with the same name.</summary>
    public sealed class PortalController : Controller
    {
        public ActionResult Index() => View();
    }
}

namespace ConsoleMVC.Tests.Mvc.AreaApp.Views.Portal
{
    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            AreaApp.Screen.Say(Renderer, "Root portal " + string.Join(" > ", Breadcrumbs));
            return RunMenu(AreaApp.Screen.Menu(("1", "Admin/Users/Index"), ("2", "Reports/Sales/Index")));
        }
    }
}

// ---- The Admin area, found from the namespace (*.Areas.Admin.Controllers)

namespace ConsoleMVC.Tests.Mvc.AreaApp.Areas.Admin.Controllers
{
    public sealed class UsersController : Controller
    {
        public ActionResult Index() => View();

        /// <summary>Redirects without naming the area: the Admin area's Portal wins over the root one.</summary>
        public ActionResult Leave() => RedirectToAction("Index", "Portal");
    }

    public sealed class PortalController : Controller
    {
        public ActionResult Index() => View();
    }
}

namespace ConsoleMVC.Tests.Mvc.AreaApp.Areas.Admin.Views.Users
{
    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            AreaApp.Screen.Say(Renderer, "Admin users " + string.Join(" > ", Breadcrumbs));
            return RunMenu(AreaApp.Screen.Menu(("p", "Portal/Index"), ("l", "Leave"), ("r", "Reports/Sales/Index")));
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.AreaApp.Areas.Admin.Views.Portal
{
    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            AreaApp.Screen.Say(Renderer, "Admin portal");
            return NavigationResult.Quit();
        }
    }
}

// ---- The Reports area, chosen with [Area] in an ordinary namespace

namespace ConsoleMVC.Tests.Mvc.AreaApp.Controllers
{
    [Area("Reports")]
    public sealed class SalesController : Controller
    {
        public ActionResult Index() => View();
    }
}

namespace ConsoleMVC.Tests.Mvc.AreaApp.Areas.Reports.Views.Sales
{
    public sealed class IndexView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            AreaApp.Screen.Say(Renderer, "Sales report in " + (NavigationHistory[^1].Area ?? "no area"));
            return NavigationResult.Quit();
        }
    }
}
