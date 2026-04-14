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
            Message = "A structured MVC framework for console applications.",
            MenuOptions = ["About", "Greeting Form", "Calculator", "Exit"]
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
                    + "while the Calculator shows simple parameter binding (int a, int b, string op).",
            MenuOptions = ["Back to Home", "Exit"]
        };

        return View(model);
    }
}
