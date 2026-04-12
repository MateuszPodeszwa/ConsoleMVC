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
    /// <returns>A <see cref="ViewResult"/> rendering the Index view with the home view model.</returns>
    public ActionResult Index()
    {
        var model = new HomeViewModel
        {
            Title = "Welcome to ConsoleMVC",
            Message = "A structured MVC framework for console applications.",
            MenuOptions = ["About", "Exit"]
        };

        return View(model);
    }

    /// <summary>
    /// Displays the about page describing the ConsoleMVC framework.
    /// </summary>
    /// <returns>A <see cref="ViewResult"/> rendering the About view with descriptive content.</returns>
    public ActionResult About()
    {
        var model = new HomeViewModel
        {
            Title = "About ConsoleMVC",
            Message = "ConsoleMVC brings ASP.NET Core MVC patterns to console applications.\n" +
                      "Each page has its own Controller, View, and ViewModel — just like web MVC.",
            MenuOptions = ["Back to Home", "Exit"]
        };

        return View(model);
    }
}
