using ConsoleMVC.App.Models;
using ConsoleMVC.Mvc;

namespace ConsoleMVC.App.Controllers;

/// <summary>
/// The default controller for the application, handling the home page and about page actions.
/// </summary>
public class HomeController : Controller
{
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
            Message = "ConsoleMVC brings ASP.NET Core MVC patterns to console applications.\n"
                    + "Each page has its own Controller, View, and Model — just like web MVC.\n"
                    + "\n"
                    + "This template demonstrates two key features:\n"
                    + "  - Navigation between controllers and actions\n"
                    + "  - Form data posting from views to controller actions\n"
                    + "\n"
                    + "The Greeting example shows complex model binding (GreetFormModel),\n"
                    + "while the Calculator shows simple parameter binding (int a, int b, string op)."
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
                new("class=\"…\"", "Applies a named style from the theme")
            ]
        };

        return View(model);
    }
}
