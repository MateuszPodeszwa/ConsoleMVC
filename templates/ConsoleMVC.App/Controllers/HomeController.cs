using ConsoleMVC.App.Models;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.App.Controllers;

/// <summary>
/// The default controller for the application, handling the home page and about page actions.
/// </summary>
/// <param name="live">Draws the progress bar while <see cref="Check"/> works; registered by the framework.</param>
public class HomeController(ILiveDisplay live) : Controller
{
    /// <summary>The number of services the status page checks.</summary>
    private const int ServiceCount = 5;

    /// <summary>
    /// Displays the main welcome page with navigation options.
    /// </summary>
    public ActionResult Index()
    {
        var model = new HomeViewModel
        {
            Title = "Welcome to ConsoleMVC",
            Message = "A structured MVC framework for console applications."
        };

        return View(model);
    }

    /// <summary>
    /// Displays the about page describing the ConsoleMVC framework.
    /// </summary>
    public ActionResult About()
    {
        var model = new HomeViewModel
        {
            Title = "About ConsoleMVC",
            Message = "ConsoleMVC brings ASP.NET Core MVC patterns to console applications.",
            Details = """
                Each page has its own **Controller**, **View** and **Model**, just like web MVC.

                This template demonstrates:

                - Navigation with `<menu>`, and a shared header rendered with `<partial>`
                - Forms with `<input>`, validated with DataAnnotations and shown with `<errors>`
                - Dependency injection: `GreetController` receives `IGreetingService`
                - Middleware in `Program.cs`, and tables, charts and live elements in the feature tour

                Run it with `dotnet watch` and edit a `.cvw` file to see *live reload*.
                This text is Markdown, drawn by `<markdown>`.
                """
        };

        return View(model);
    }

    /// <summary>
    /// Displays a table of the C-View Markup elements used by this template.
    /// </summary>
    public ActionResult Features()
    {
        var model = new FeaturesViewModel
        {
            Features =
            [
                new("<box>", "A bordered panel with an optional title"),
                new("<text>", "Styled text that wraps to the available width"),
                new("<separator>", "A horizontal rule"),
                new("<table>", "Rows and cells with columns sized to the content"),
                new("<input>", "Reads a value into the view's Form"),
                new("<menu>", "Shows options, reads the choice and navigates"),
                new("<errors>", "Shows validation messages for the form"),
                new("<partial>", "Renders a reusable view from Views/Shared"),
                new("<columns>", "Places columns side by side, stacking them on narrow terminals"),
                new("<progress>", "A progress bar, kept up to date while an action works"),
                new("<spinner>", "Turns while an action works, then shows a tick"),
                new("<bar-chart>", "Compares values as horizontal bars"),
                new("<sparkline>", "Draws a series of values on one line"),
                new("<markdown>", "Draws Markdown: headings, lists, emphasis, code and links"),
                new("class=\"…\"", "Applies a named style from the theme")
            ]
        };

        return View(model);
    }

    /// <summary>
    /// Checks the services, showing a progress bar while it works, then shows the status page.
    /// </summary>
    /// <param name="cancellationToken">Stops the check when the application stops.</param>
    public async Task<ActionResult> Check(CancellationToken cancellationToken)
    {
        var progress = new ProgressElement { Label = "Checking services ", Max = ServiceCount };

        // The bar is redrawn in place on each update, and stays on the screen when the region ends
        await using (var region = live.Start(progress))
        {
            for (var i = 0; i < ServiceCount; i++)
            {
                await Task.Delay(150, cancellationToken);
                region.Update(() => progress.Value++);
            }
        }

        return RedirectToAction("Status");
    }

    /// <summary>
    /// Displays the status page; its view refreshes every second with <c>@refresh</c>, running this action again.
    /// </summary>
    public ActionResult Status()
    {
        var now = DateTime.Now;

        // Made-up numbers that change with the clock, so each refresh draws something new
        var model = new StatusViewModel
        {
            Time = now,
            Healthy = ServiceCount,
            Total = ServiceCount,
            Load = Enumerable.Range(0, 30).Select(i => (int)(50 + 40 * Math.Sin((now.Second + i) / 4.0))).ToList(),
            Timings =
            [
                new("Catalogue", 40 + now.Second % 7 * 5),
                new("Orders", 90 + now.Second % 5 * 12),
                new("Payments", 150 + now.Second % 3 * 20),
                new("Search", 25 + now.Second % 4 * 8),
                new("Mail", 60 + now.Second % 6 * 9)
            ]
        };

        return View(model);
    }
}
