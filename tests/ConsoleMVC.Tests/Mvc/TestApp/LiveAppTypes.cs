using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

// Controllers and views for live elements and timed refresh (docs/design/live-elements.md).

namespace ConsoleMVC.Tests.Mvc.LiveApp
{
    /// <summary>Counts the dashboard's visits, so a refresh can be told from the first visit.</summary>
    public sealed class VisitCounter
    {
        public int Visits { get; set; }
    }
}

namespace ConsoleMVC.Tests.Mvc.LiveApp.Controllers
{
    using ConsoleMVC.Tests.Mvc.LiveApp;

    public sealed class DashboardController(VisitCounter counter, ILiveDisplay live) : Controller
    {
        /// <summary>Refreshes every second, chosen by the action.</summary>
        public ActionResult Index() => View(++counter.Visits).RefreshEvery(TimeSpan.FromSeconds(1));

        /// <summary>Refreshes because its view says so (as <c>@refresh</c> does).</summary>
        public ActionResult Clock() => View(++counter.Visits);

        /// <summary>The view would refresh, but the action turns it off with NoRefresh.</summary>
        public ActionResult Still() => View("Clock", ++counter.Visits).NoRefresh();

        /// <summary>Shows a progress bar while it works.</summary>
        public async Task<ActionResult> Import()
        {
            var progress = new ProgressElement { Label = "Import ", Max = 3, BarWidth = 6 };
            await using (var region = live.Start(progress))
            {
                for (var i = 0; i < 3; i++)
                {
                    await Task.Yield();
                    region.Update(() => progress.Value++);
                }
            }

            return View();
        }
    }
}

namespace ConsoleMVC.Tests.Mvc.LiveApp.Views.Dashboard
{
    public sealed class IndexView : ConsoleView<int>
    {
        public override NavigationResult Render(int Model)
        {
            var text = new TextElement();
            text.AppendText($"Visits: {Model}");
            Renderer.Render(text);

            var menu = new MenuElement();
            foreach (var (key, target) in new[] { ("c", "Clock"), ("s", "Still"), ("i", "Import") })
            {
                var option = new MenuOptionElement { Key = key, Target = target };
                option.AppendText(target);
                menu.Add(option);
            }

            var quit = new MenuOptionElement { Key = "q", Quit = true };
            quit.AppendText("Quit");
            menu.Add(quit);
            return RunMenu(menu);
        }
    }

    public sealed class ClockView : ConsoleView<int>
    {
        protected override TimeSpan? RefreshInterval => TimeSpan.FromMilliseconds(500);

        public override NavigationResult Render(int Model)
        {
            var text = new TextElement();
            text.AppendText($"Tick {Model}");
            Renderer.Render(text);
            return Input.ReadLine() is null ? NavigationResult.Quit() : NavigationResult.Back();
        }
    }

    public sealed class ImportView : ConsoleView<object>
    {
        public override NavigationResult Render(object Model)
        {
            var text = new TextElement();
            text.AppendText("Imported");
            Renderer.Render(text);
            return NavigationResult.Quit();
        }
    }
}
